using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Storefront.Web.Services;

/// <summary>One-time messages shown as toasts after a redirect.</summary>
public static class ToastExtensions
{
    /// <summary>TempData key for the toast kind (success, danger, warning, info).</summary>
    public const string KindKey = "toast.kind";

    /// <summary>TempData key for the toast text.</summary>
    public const string TextKey = "toast.text";

    /// <summary>Queues a success toast.</summary>
    public static void Success(this ITempDataDictionary tempData, string text) => Set(tempData, "success", text);

    /// <summary>Queues an error toast.</summary>
    public static void Error(this ITempDataDictionary tempData, string text) => Set(tempData, "danger", text);

    /// <summary>Queues a warning toast.</summary>
    public static void Warning(this ITempDataDictionary tempData, string text) => Set(tempData, "warning", text);

    /// <summary>Queues an informational toast.</summary>
    public static void Info(this ITempDataDictionary tempData, string text) => Set(tempData, "info", text);

    private static void Set(ITempDataDictionary tempData, string kind, string text)
    {
        tempData[KindKey] = kind;
        tempData[TextKey] = text;
    }
}
