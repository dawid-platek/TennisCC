using Microsoft.AspNetCore.Components;

namespace TenisProgress.Client.Services;

public enum ToastKind { Success, Error }

/// <summary>A message bound to the page (<see cref="Path"/>) it should be shown on.</summary>
public sealed record Toast(ToastKind Kind, string Text, string Path, bool Sticky);

/// <summary>App-wide flash messages. Call before NavigateTo with the target path to show it there.</summary>
public sealed class Toasts(NavigationManager nav)
{
    public Toast? Current { get; private set; }
    public event Action? Changed;

    public void Success(string text, string? path = null, bool sticky = false) => Show(ToastKind.Success, text, path, sticky);
    public void Error(string text) => Show(ToastKind.Error, text, null, false);
    public void Errors(IEnumerable<string> errors) => Error(string.Join(" ", errors));

    public void Clear()
    {
        Current = null;
        Changed?.Invoke();
    }

    /// <summary>Called by the layout on navigation: drops messages meant for other pages.</summary>
    public void OnNavigated(string path)
    {
        if (Current is { Sticky: false } t && t.Path != path) Clear();
    }

    public static string PathOf(NavigationManager nav, string uri) => nav.ToBaseRelativePath(uri).Split('?', '#')[0].TrimEnd('/');

    private void Show(ToastKind kind, string text, string? path, bool sticky)
    {
        Current = new Toast(kind, text, path?.TrimEnd('/') ?? PathOf(nav, nav.Uri), sticky);
        Changed?.Invoke();
    }
}
