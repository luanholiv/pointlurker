using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class UpsertStoreProfileValuesTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly ISecretProtector _protector = Substitute.For<ISecretProtector>();
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpsertStoreProfileValues _sut;

    public UpsertStoreProfileValuesTests()
    {
        _protector.Protect(Arg.Any<string>()).Returns(call => $"ct:{call.Arg<string>()}");
        _sut = new UpsertStoreProfileValues(
            _storeRepository,
            _protector,
            _unitOfWork,
            _timeProvider
        );
    }

    [Fact]
    public async Task Returns_not_found_when_store_missing()
    {
        _storeRepository
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WatchedStore?)null);

        var result = await _sut.ExecuteAsync(
            Guid.NewGuid(),
            new[] { new StoreProfileValueUpsert("Label", "v") }
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.NotFound);
    }

    [Fact]
    public async Task Encrypts_and_persists_values()
    {
        var storeId = Guid.NewGuid();
        var store = CreateStore(storeId);
        _storeRepository.GetForUpdateAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(
            storeId,
            new[] { new StoreProfileValueUpsert("Twitch Username", "xqc") }
        );

        result.IsSuccess.Should().BeTrue();
        store.InputValues.Should().ContainSingle();
        store.GetCiphertext("Twitch Username").Should().Be("ct:xqc");
        _storeRepository.Received(1).Update(store);
        // One commit for the batch, however many values it carried.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Null_value_removes_existing_label()
    {
        var storeId = Guid.NewGuid();
        var store = CreateStore(storeId);
        store.UpsertValue("Twitch Username", "old", DateTimeOffset.UtcNow);
        _storeRepository.GetForUpdateAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(
            storeId,
            new[] { new StoreProfileValueUpsert("Twitch Username", null) }
        );

        result.IsSuccess.Should().BeTrue();
        store.InputValues.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_canonical_label_is_skipped()
    {
        var storeId = Guid.NewGuid();
        var store = CreateStore(storeId);
        _storeRepository.GetForUpdateAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(
            storeId,
            new[] { new StoreProfileValueUpsert("   ", "v") }
        );

        result.IsSuccess.Should().BeTrue();
        store.InputValues.Should().BeEmpty();
    }

    private static WatchedStore CreateStore(Guid id) =>
        new(
            id,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "S",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
}
