using System.Windows.Automation;

namespace VirtualKeyboard.App.Services;

/// <summary>
/// Detects when the user focuses an editable text field in another process.
/// Called from a background thread: UI Automation is cross-process and must
/// never block the dispatcher.
/// </summary>
public static class FocusProbe
{
    /// <summary>
    /// Returns a stable key for the focused text field, or null when focus is
    /// not on something that accepts typed text.
    /// </summary>
    public static string? TryGetFocusedTextTarget()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null)
            {
                return null;
            }

            var current = element.Current;
            if (current.ProcessId == Environment.ProcessId)
            {
                return null;
            }

            var controlType = current.ControlType;
            var isEdit = controlType == ControlType.Edit;
            var isEditableDocument = controlType == ControlType.Document
                                     && element.TryGetCurrentPattern(ValuePattern.Pattern, out _);

            if (!isEdit && !isEditableDocument)
            {
                return null;
            }

            if (!current.IsKeyboardFocusable)
            {
                return null;
            }

            var runtimeId = element.GetRuntimeId();
            return current.ProcessId + ":" + string.Join(",", runtimeId);
        }
        catch
        {
            // UIA throws for hung, closing or elevated windows. Treat as "no text focus".
            return null;
        }
    }
}
