using Microsoft.JSInterop;
using PackageManager.Shared;

namespace PackageManager.Client.Services;

/// <summary>Who is signed in. Guests can browse and download; only the admin can change anything.</summary>
public class AuthState(Api api)
{
    public bool IsAdmin { get; private set; }
    public string? UserName { get; private set; }
    public bool Loaded { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync()
    {
        try { Set(await api.Me()); }
        catch (ApiException) { Set(new AuthStateDto()); }
        Loaded = true;
        Changed?.Invoke();
    }

    public async Task LoginAsync(string user, string password) =>
        Set(await api.Login(new LoginRequest { UserName = user, Password = password }), notify: true);

    public async Task LogoutAsync()
    {
        await api.Logout();
        Set(new AuthStateDto(), notify: true);
    }

    private void Set(AuthStateDto dto, bool notify = false)
    {
        IsAdmin = dto.IsAdmin;
        UserName = dto.UserName;
        if (notify) Changed?.Invoke();
    }
}

public class ThemeState(IJSRuntime js)
{
    public bool IsDark { get; private set; } = true;
    public event Action? Changed;

    public async Task LoadAsync()
    {
        IsDark = await js.InvokeAsync<string?>("pm.store.get", "pm.theme") != "light";
        Changed?.Invoke();
    }

    public async Task ToggleAsync()
    {
        IsDark = !IsDark;
        await js.InvokeVoidAsync("pm.setTheme", IsDark ? "dark" : "light");
        Changed?.Invoke();
    }
}

/// <summary>Remembers whether Verdaccio answered, so the UPM tab can show a red dot.</summary>
public class UpmStatus
{
    public bool Unavailable { get; private set; }
    public event Action? Changed;

    public void Report(bool available)
    {
        if (Unavailable == !available) return;
        Unavailable = !available;
        Changed?.Invoke();
    }
}
