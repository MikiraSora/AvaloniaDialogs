﻿using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DialogHostAvalonia;
using System;
using System.Threading.Tasks;

namespace AvaloniaDialogs.Views;

/// <summary>
/// A user-control which is meant to pop on the screen for some user action, and optionally returns a result..
/// </summary>
/// <remarks>For dialogs that return a result, inherit from <seealso cref="BaseDialog{TResult}"/> instead for type safety.</remarks>
public abstract class BaseDialog : UserControl
{
    private IInputElement? previousFocus;
    private TaskCompletionSource<object?>? showNestedTask;

    /// <summary>
    /// Called before closing this dialog. Return false to cancel.
    /// </summary>
    public virtual bool OnClosing()
    {
        return true;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        previousFocus?.Focus();
    }

    /// <summary>
    /// Shows this dialog and waits until it's closed.
    /// If you want the dialog's result in a type-safe way, use <see cref="BaseDialog{TResult}.ShowAsync"/> instead.
    /// </summary>
    public async Task<object?> ShowAsync(string? dialogIdentifier = null)
    {
        DialogSession? currentSession = DialogHost.GetDialogSession(dialogIdentifier);
        if (currentSession == null)
        {
            return await DialogHost.Show(this, dialogIdentifier);
        }
        else
        {
            return await ShowNestedAsync(currentSession);
        }
    }

    private async Task<object?> ShowNestedAsync(DialogSession session)
    {
        object? previousContent = session.Content;
        showNestedTask = new((session, previousContent));
        session.UpdateContent(this);
        return await showNestedTask.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Closes this dialog without returning a result.
    /// </summary>
    public void Close()
    {
        Close(null);
    }

    protected void Close(object? result)
    {
        if (showNestedTask != null)
        {
            (DialogSession session, object? previousContent) = (ValueTuple<DialogSession, object?>)showNestedTask.Task.AsyncState!;
            if (previousContent != null)
            {
                session.UpdateContent(previousContent);
            }
            showNestedTask.TrySetResult(result);
            showNestedTask = null;
        }
        else
        {
            DialogHost.Close(null, result);
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        DoInitialFocus();
    }

    /// <summary>
    /// Focus something in order to remove the focus from behind the dialog, otherwise the user may still interact with background elements using the keyboard
    /// </summary>
    /// <remarks>The default implementation focuses the first focusable child using DFS. If no such element was found, nothing is focused - this is undesirable behavior!</remarks>
    protected virtual void DoInitialFocus()
    {
        IFocusManager? focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        previousFocus = focusManager?.GetFocusedElement();
        InputElement? firstFocusableDescandant = (InputElement?)UIUtil.SelectLogicalDescendant(this, x => x is InputElement inputElement && inputElement.Focusable);
        firstFocusableDescandant?.FocusEventually();
    }
}

/// <summary>
/// A user-control which is meant to pop on the screen for some user action, and optionally returns a result.
/// </summary>
public abstract class BaseDialog<TResult> : BaseDialog
{
    /// <summary>
    /// Shows this dialog and returns the result, or null if it was closed without a result (e.g. by an "Escape" press).
    /// </summary>
    public new async Task<Optional<TResult>> ShowAsync(string? dialogIdentifier = null)
    {
        object? result = await base.ShowAsync(dialogIdentifier);
        if (result == null)
            return Optional<TResult>.Empty;
        return new Optional<TResult>((TResult)result);
    }

    protected void Close(TResult result)
    {
        base.Close(result);
    }
}