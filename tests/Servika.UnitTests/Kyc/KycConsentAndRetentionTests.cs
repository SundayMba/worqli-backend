using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Catalogue;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.UnitTests.Kyc;

public class KycConsentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static SubmitKycRequest Request(string? consent, IReadOnlyList<PoseSelfieRequest>? poses = null) =>
        new("Nin", "12345678901", "AAAA", "AAAA", poses, consent);

    // The consent check runs before any dependency is touched, so the handler can be built bare.
    private static SubmitKycHandler BareHandler() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handler_refuses_a_submission_without_consent(string? consent)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => BareHandler().HandleAsync(Guid.NewGuid(), Request(consent), CancellationToken.None));
        Assert.Equal("Agree to the consent before sending.", ex.Message);
    }

    [Fact]
    public void Domain_refuses_blank_consent_and_records_a_real_one()
    {
        var k = ArtisanKyc.Submit(Guid.NewGuid(), KycIdType.Nin, null, "s", "i", Now);
        Assert.Throws<ArgumentException>(() => k.RecordConsent(" ", Now));
        k.RecordConsent("kyc-2026-11", Now);
        Assert.Equal("kyc-2026-11", k.ConsentVersion);
        Assert.Equal(Now, k.ConsentedAtUtc);
    }

    [Fact]
    public void Pose_validation_accepts_smile_and_rejects_unknown_poses()
    {
        SubmitKycHandler.ValidatePoses(new[]
        {
            new PoseSelfieRequest("left", "x"), new PoseSelfieRequest("right", "x"),
            new PoseSelfieRequest("up", "x"), new PoseSelfieRequest("Smile", "x"),
        });
        Assert.Throws<ArgumentException>(() => SubmitKycHandler.ValidatePoses(new[] { new PoseSelfieRequest("wink", "x") }));
    }

    [Fact]
    public void Pose_validation_allows_at_most_four()
    {
        var five = Enumerable.Range(0, 5).Select(_ => new PoseSelfieRequest("left", "x")).ToArray();
        Assert.Throws<ArgumentException>(() => SubmitKycHandler.ValidatePoses(five));
    }

    [Fact]
    public void Liveness_keeps_only_known_challenges_and_rejects_unknown_methods()
    {
        var k = ArtisanKyc.Submit(Guid.NewGuid(), KycIdType.Nin, null, "s", "i", Now);
        k.RecordLiveness("On-Device", true, new[] { "straight", "LEFT", "right", "smile", "jump" }, 2);
        Assert.Equal("on-device", k.LivenessMethod);
        Assert.Equal(new[] { "straight", "left", "right", "smile" }, k.LivenessChallenges);
        Assert.Equal(2, k.LivenessAttempts);
        Assert.Throws<ArgumentException>(() => k.RecordLiveness("cloud", true, null, 1));
    }
}

public class KycRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ArtisanVerificationStatus.Verified, 23, false)]
    [InlineData(ArtisanVerificationStatus.Verified, 24, true)]
    [InlineData(ArtisanVerificationStatus.Rejected, 24 * 29, false)]
    [InlineData(ArtisanVerificationStatus.Rejected, 24 * 30, true)]
    [InlineData(ArtisanVerificationStatus.Pending, 24 * 365, false)]
    public void Due_by_status_and_age(ArtisanVerificationStatus status, int hoursSinceReview, bool due)
    {
        Assert.Equal(due, ArtisanKyc.IsSelfieDeletionDue(status, Now.AddHours(-hoursSinceReview), null, Now));
    }

    [Fact]
    public void Never_due_when_unreviewed_or_already_deleted()
    {
        Assert.False(ArtisanKyc.IsSelfieDeletionDue(ArtisanVerificationStatus.Verified, null, null, Now));
        Assert.False(ArtisanKyc.IsSelfieDeletionDue(ArtisanVerificationStatus.Verified, Now.AddDays(-5), Now.AddDays(-4), Now));
    }

    [Fact]
    public async Task Sweep_deletes_selfie_and_pose_files_keeps_the_id_and_stamps_the_time()
    {
        var k = ArtisanKyc.Submit(Guid.NewGuid(), KycIdType.Nin, null, "selfie-key", "id-key", Now.AddDays(-3));
        k.SetPoseSelfies("[{\"Pose\":\"left\",\"Key\":\"left-key\"},{\"Pose\":\"smile\",\"Key\":\"smile-key\"}]");
        k.Approve(Now.AddDays(-2));
        var storage = new FakeStorage();
        var repo = new FakeKycRepo(k);

        var cleaned = await new KycImageRetentionHandler(repo, storage).RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, cleaned);
        Assert.Equal(new[] { "selfie-key", "left-key", "smile-key" }, storage.Deleted);
        Assert.Equal("", k.SelfieKey);
        Assert.Null(k.PoseSelfiesJson);
        Assert.Equal("id-key", k.IdDocumentKey);
        Assert.Equal(Now, k.SelfieImagesDeletedAtUtc);
        Assert.Equal(1, repo.Saves);
    }

    [Fact]
    public void Resubmitting_clears_the_deleted_stamp()
    {
        var k = ArtisanKyc.Submit(Guid.NewGuid(), KycIdType.Nin, null, "s", "i", Now.AddDays(-40));
        k.Reject("blurry", Now.AddDays(-31));
        k.MarkSelfieImagesDeleted(Now.AddDays(-1));
        k.Resubmit(KycIdType.Nin, null, "s2", "i2", Now);
        Assert.Null(k.SelfieImagesDeletedAtUtc);
        Assert.Equal("s2", k.SelfieKey);
    }

    private sealed class FakeStorage : IFileStorage
    {
        public List<string> Deleted { get; } = new();
        public Task<string> SaveAsync(byte[] content, string contentType, CancellationToken ct) => Task.FromResult(Guid.NewGuid().ToString());
        public Task<StoredFile?> GetAsync(string key, CancellationToken ct) => Task.FromResult<StoredFile?>(null);
        public Task DeleteAsync(string key, CancellationToken ct) { Deleted.Add(key); return Task.CompletedTask; }
    }

    private sealed class FakeKycRepo : IArtisanKycRepository
    {
        private readonly List<ArtisanKyc> _all;
        public int Saves { get; private set; }
        public FakeKycRepo(params ArtisanKyc[] all) => _all = all.ToList();
        public void Add(ArtisanKyc submission) => _all.Add(submission);
        public Task<ArtisanKyc?> GetForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(_all.FirstOrDefault(k => k.UserId == userId));
        public Task<IReadOnlyList<ArtisanKyc>> ListAsync(ArtisanVerificationStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<ArtisanKyc>>(_all);
        public Task<ArtisanKyc?> GetByIdForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult(_all.FirstOrDefault(k => k.Id == id));
        public Task<IReadOnlyList<ArtisanKyc>> ListSelfieRetentionDueAsync(DateTimeOffset verifiedBefore, DateTimeOffset rejectedBefore, int max, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArtisanKyc>>(_all.Where(k => k.SelfieImagesDeletedAtUtc == null).ToList());
        public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(1); }
    }
}
