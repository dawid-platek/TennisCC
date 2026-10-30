using Microsoft.JSInterop;

namespace TenisProgress.Client.Services;

public static class JsExtensions
{
    public static ValueTask<bool> ConfirmAsync(this IJSRuntime js, string message) => js.InvokeAsync<bool>("confirm", message);
}
