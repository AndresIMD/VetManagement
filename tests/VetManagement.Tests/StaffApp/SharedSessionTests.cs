using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using VetManagement.Staff.UI.Services.Auth;

namespace VetManagement.Tests.StaffApp;

/// <summary>
/// One session per browser: every tab of the staff app uses it, a new tab finds it, and no tab ends another's. A session
/// kept only until the browser closes still ends then. Production 2026-11: a tab signed in without "Remember me" wiped
/// the session of a tab signed in with it, which went on showing pages whose data never loaded.
/// </summary>
public class SharedSessionTests
{
    private readonly FakeAccountApi _api = new();

    private async Task<(FakeTab Tab, SessionRefresher Session)> OpenWatchingTabAsync(FakeBrowser browser, string path = "items")
    {
        var tab = browser.OpenTab(path);
        var session = tab.CreateRefresher();
        await session.WatchAsync();
        return (tab, session);
    }

    /// <summary>What the sign-in page does.</summary>
    private static async Task SignInAsync(FakeTab tab, string user, bool rememberMe)
    {
        (await tab.Account().LoginAsync(user, "pw", rememberMe)).Should().NotBeNull();
        tab.Auth.NotifyUserAuthentication();
    }

    private static async Task<string?> UserOfAsync(FakeTab tab)
        => await tab.TokenAsync() is { } token ? new JwtSecurityTokenHandler().ReadJwtToken(token).Subject : null;

    /// <summary>Holds the next refresh at the API until the returned action releases it.</summary>
    private async Task<(Task Refresh, Action Answer)> StartHeldRefreshAsync(SessionRefresher session)
    {
        var hold = _api.HoldRefresh = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = _api.RefreshArrived.Task;
        var refresh = session.RefreshAsync();
        await arrived;
        return (refresh, () => hold.SetResult());
    }

    [Fact]
    public async Task ANewTab_IsSignedIn_WhenAnotherTabSignedInWithoutRememberMe()
    {
        var browser = new FakeBrowser(_api);
        await browser.OpenTab().Account().LoginAsync("ana", "pw", rememberMe: false);

        var second = browser.OpenTab();

        (await second.TokenAsync()).Should().NotBeNull("the tabs of one browser share its session");
    }

    [Fact]
    public async Task MixingRememberMe_InOneBrowser_KeepsEveryTabSignedIn()
    {
        var browser = new FakeBrowser(_api);
        var first = browser.OpenTab();
        await first.Account().LoginAsync("ana", "pw", rememberMe: false);
        var second = browser.OpenTab();
        await second.Account().LoginAsync("ana", "pw", rememberMe: true);

        await first.CreateRefresher().RefreshAsync(); // its per-minute refresh

        (await second.TokenAsync()).Should().NotBeNull("one tab's refresh must not end another tab's session");
        (await first.TokenAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task WithoutRememberMe_TheSessionEndsWithTheBrowser()
    {
        var browser = new FakeBrowser(_api);
        await browser.OpenTab().Account().LoginAsync("ana", "pw", rememberMe: false);
        (await browser.OpenTab().TokenAsync()).Should().NotBeNull();

        browser.Close();

        (await browser.OpenTab().TokenAsync()).Should().BeNull();
        browser.LocalStorage.Should().BeEmpty("the closed browser's session is cleared, not just hidden");
    }

    [Fact]
    public async Task WithRememberMe_TheSessionOutlivesTheBrowser()
    {
        var browser = new FakeBrowser(_api);
        await browser.OpenTab().Account().LoginAsync("ana", "pw", rememberMe: true);

        browser.Close();

        (await UserOfAsync(browser.OpenTab())).Should().Be("ana");
    }

    [Fact]
    public async Task ASessionAnEarlierVersionKeptInATab_BecomesTheBrowsers_UntilItCloses()
    {
        var browser = new FakeBrowser(_api);
        var tab = browser.OpenTab();
        var token = _api.Token("ana");
        tab.SessionStorage["authToken"] = token; // signed in without "Remember me" before the update

        (await tab.TokenAsync()).Should().Be(token, "nobody signs in again after the update");
        (await browser.OpenTab().TokenAsync()).Should().Be(token);
        tab.SessionStorage.Should().BeEmpty();

        browser.Close();
        (await browser.OpenTab().TokenAsync()).Should().BeNull("it wasn't remembered");
    }

    [Fact]
    public async Task SigningOutInOneTab_SendsTheOthersToSignIn()
    {
        var browser = new FakeBrowser(_api);
        var (first, _) = await OpenWatchingTabAsync(browser);
        await SignInAsync(first, "ana", rememberMe: false);
        var (second, _) = await OpenWatchingTabAsync(browser);

        await second.Account().LogoutAsync();
        await browser.DeliverStorageEventsAsync();

        first.Navigation.Visits.Should().Equal(("http://staff/login", true));
    }

    [Fact]
    public async Task AnotherUserSigningIn_ReloadsTheOtherTabs_SoNothingIsSavedAsThePreviousUser()
    {
        var browser = new FakeBrowser(_api);
        var (first, _) = await OpenWatchingTabAsync(browser, "items");
        await SignInAsync(first, "ana", rememberMe: false);
        var (second, _) = await OpenWatchingTabAsync(browser, "login");

        await second.Account().LogoutAsync();
        await SignInAsync(second, "ben", rememberMe: true);
        await browser.DeliverStorageEventsAsync();

        first.Navigation.Visits.Should().Equal([("http://staff/items", true)], "it reloads as Ben instead of going on as Ana");
        second.Navigation.Visits.Should().BeEmpty("the tab that signed in carries on");
    }

    [Fact]
    public async Task ATabShowingTheSignInPage_GoesIn_WhenAnotherTabSignsIn()
    {
        var browser = new FakeBrowser(_api);
        var (first, _) = await OpenWatchingTabAsync(browser, "login");
        var (second, _) = await OpenWatchingTabAsync(browser, "login");

        await SignInAsync(second, "ana", rememberMe: false);
        await browser.DeliverStorageEventsAsync();

        first.Navigation.Visits.Should().Equal(("http://staff/", true));
    }

    [Fact]
    public async Task ARefreshInAnotherTab_LeavesThisTabAlone_UnlessTheRolesChanged()
    {
        var browser = new FakeBrowser(_api);
        var (first, _) = await OpenWatchingTabAsync(browser);
        await SignInAsync(first, "ana", rememberMe: false);
        var (_, secondSession) = await OpenWatchingTabAsync(browser);
        var menuUpdates = 0;
        first.Auth.AuthenticationStateChanged += _ => menuUpdates++;

        await secondSession.RefreshAsync();
        await browser.DeliverStorageEventsAsync();
        menuUpdates.Should().Be(0);

        _api.Roles["ana"] = ["Supervisor"];
        await secondSession.RefreshAsync();
        await browser.DeliverStorageEventsAsync();
        menuUpdates.Should().Be(1, "the new role reaches this tab's menus");
        first.Navigation.Visits.Should().BeEmpty();
    }

    [Fact]
    public async Task ARefreshAnsweredAfterASignOutElsewhere_DoesNotSignTheBrowserBackIn()
    {
        var browser = new FakeBrowser(_api);
        var (first, firstSession) = await OpenWatchingTabAsync(browser);
        await SignInAsync(first, "ana", rememberMe: false);
        var (refresh, answer) = await StartHeldRefreshAsync(firstSession);

        await browser.OpenTab().Account().LogoutAsync();
        answer();
        await refresh;

        browser.LocalStorage.Should().BeEmpty();
        first.Navigation.Visits.Should().Equal(("http://staff/login", true));
    }

    [Fact]
    public async Task ARefreshAnsweredAfterAnotherSignIn_KeepsTheNewSession()
    {
        var browser = new FakeBrowser(_api);
        var (first, firstSession) = await OpenWatchingTabAsync(browser);
        await SignInAsync(first, "ana", rememberMe: false);
        var (refresh, answer) = await StartHeldRefreshAsync(firstSession);

        await browser.OpenTab().Account().LoginAsync("ben", "pw", rememberMe: true);
        answer();
        await refresh;

        (await UserOfAsync(first)).Should().Be("ben");
        first.Navigation.Visits.Should().Equal(("http://staff/items", true));
    }

    [Fact]
    public async Task TheApiEndingASession_ThatAnotherTabAlreadyReplaced_LeavesTheNewOne()
    {
        var browser = new FakeBrowser(_api);
        var (first, firstSession) = await OpenWatchingTabAsync(browser);
        await SignInAsync(first, "ana", rememberMe: false);
        _api.Ended.Add("ana"); // e.g. an admin blocked Ana
        var (refresh, answer) = await StartHeldRefreshAsync(firstSession);

        await browser.OpenTab().Account().LoginAsync("ben", "pw", rememberMe: false);
        answer();
        await refresh;

        (await UserOfAsync(first)).Should().Be("ben", "Ana's 401 is not about Ben's session");
    }

    [Fact]
    public async Task TheApiEndingTheSession_SignsEveryTabOut()
    {
        var browser = new FakeBrowser(_api);
        var (first, firstSession) = await OpenWatchingTabAsync(browser);
        await SignInAsync(first, "ana", rememberMe: true);
        var (second, _) = await OpenWatchingTabAsync(browser);
        _api.Ended.Add("ana");

        await firstSession.RefreshAsync();
        await browser.DeliverStorageEventsAsync();

        browser.LocalStorage.Should().BeEmpty();
        first.Navigation.Visits.Should().Equal(("http://staff/login", true));
        second.Navigation.Visits.Should().Equal(("http://staff/login", true));
    }

    [Fact]
    public async Task SigningInInThisTab_DoesNotReloadIt()
    {
        var browser = new FakeBrowser(_api);
        var (tab, _) = await OpenWatchingTabAsync(browser, "login");
        await SignInAsync(tab, "ana", rememberMe: false);
        var (_, otherSession) = await OpenWatchingTabAsync(browser);

        await otherSession.RefreshAsync();
        await browser.DeliverStorageEventsAsync();

        tab.Navigation.Visits.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true)] // index.html cached from an earlier version, without js/session.js
    [InlineData(true, false)] // a browser that refuses cookies
    public async Task WithoutTheBrowserCookie_ASessionNotRemembered_LastsUntilItsTokenExpires(bool hasSessionScript, bool cookiesEnabled)
    {
        var browser = new FakeBrowser(_api) { HasSessionScript = hasSessionScript, CookiesEnabled = cookiesEnabled };
        var (tab, _) = await OpenWatchingTabAsync(browser);
        await SignInAsync(tab, "ana", rememberMe: false);
        (await UserOfAsync(browser.OpenTab())).Should().Be("ana");

        browser.Close();

        (await UserOfAsync(browser.OpenTab())).Should().Be("ana", "an error or a missing cookie never signs anyone out");
    }
}
