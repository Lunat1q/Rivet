using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rivet.App.Updates;
using Rivet.App.ViewModels;

namespace Rivet.App.Tests;

public class VersionParsingTests
{
    [Theory]
    [InlineData("1.2.0", 1, 2, 0)]
    [InlineData("v1.2.0", 1, 2, 0)]
    [InlineData("V0.2.0", 0, 2, 0)]
    public void Parses_plain_and_v_prefixed_tags(string tag, int major, int minor, int build)
    {
        Assert.True(GitHubUpdateChecker.TryParseVersion(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("latest")]
    [InlineData("v")]
    public void Rejects_tags_it_cannot_understand(string? tag)
    {
        Assert.False(GitHubUpdateChecker.TryParseVersion(tag, out _));
    }
}

public class UpdateInfoTests
{
    [Fact]
    public void SizeText_is_megabytes()
    {
        var info = new UpdateInfo(
            new Version(1, 0, 0), "notes", new Uri("https://x/setup.exe"), null, 5 * 1024 * 1024);
        Assert.Equal("5 MB", info.SizeText);
    }
}

public class UpdateViewModelTests
{
    private readonly IUpdateChecker _checker = Substitute.For<IUpdateChecker>();
    private readonly IUpdateInstaller _installer = Substitute.For<IUpdateInstaller>();

    private UpdateViewModel Build() =>
        new(_checker, _installer, NullLogger<UpdateViewModel>.Instance);

    private static UpdateInfo AnUpdate() =>
        new(new Version(9, 9, 9), "notes", new Uri("https://x/setup.exe"), null, 1024 * 1024);

    [Fact]
    public void Not_offered_before_a_check_runs()
    {
        Assert.False(Build().IsOffered);
    }

    [Fact]
    public async Task Offers_an_available_update_after_the_check()
    {
        _checker.CheckAsync().Returns(AnUpdate());
        var vm = Build();

        await vm.CheckInBackgroundAsync();

        Assert.True(vm.IsOffered);
        Assert.Contains("9.9.9", vm.Headline);
    }

    [Fact]
    public async Task Nothing_offered_when_already_current()
    {
        _checker.CheckAsync().Returns((UpdateInfo?)null);
        var vm = Build();

        await vm.CheckInBackgroundAsync();

        Assert.False(vm.IsOffered);
        Assert.Equal(string.Empty, vm.Headline);
    }

    [Fact]
    public async Task A_check_failure_is_swallowed()
    {
        _checker.CheckAsync().Returns<Task<UpdateInfo?>>(_ => throw new InvalidOperationException("offline"));
        var vm = Build();

        await vm.CheckInBackgroundAsync(); // must not throw
        Assert.False(vm.IsOffered);
    }

    [Fact]
    public async Task Dismiss_hides_the_offer()
    {
        _checker.CheckAsync().Returns(AnUpdate());
        var vm = Build();
        await vm.CheckInBackgroundAsync();

        vm.DismissCommand.Execute(null);

        Assert.False(vm.IsOffered);
    }
}
