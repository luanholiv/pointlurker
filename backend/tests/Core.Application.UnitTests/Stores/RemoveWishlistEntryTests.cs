using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace Core.Application.UnitTests.Stores;

public sealed class RemoveWishlistEntryTests
{
    private readonly IWishlistEntryRepository _wishlistEntryRepository =
        Substitute.For<IWishlistEntryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RemoveWishlistEntry _sut;

    public RemoveWishlistEntryTests()
    {
        _sut = new RemoveWishlistEntry(_wishlistEntryRepository, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_removes_existing_entry()
    {
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );

        _wishlistEntryRepository
            .GetForUpdateAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);

        var result = await _sut.ExecuteAsync(entry.Id);

        result.IsSuccess.Should().BeTrue();
        _wishlistEntryRepository.Received(1).Remove(entry);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_when_not_found()
    {
        _wishlistEntryRepository
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.EntryNotFound);
        _wishlistEntryRepository.DidNotReceive().Remove(Arg.Any<WishlistEntry>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
