## v0.1.8

- Adds a screen-edge tab that smoothly tucks the message panel aside while keeping new-activity notifications available.

## v0.1.7

- Shows themed tooltips quickly above enabled icons whenever screen space allows, while disabled actions stay quiet.

## v0.1.6

- Explains which file the Open file action will open and why it was selected, while direct file links show their exact target.
- Makes the compact installer verify and install the required Microsoft .NET runtime more reliably, including clear restart and failure handling.
- Adds a separate action for copying the readable version of structured messages.
- Separates approval details, planned actions, and transcript events so mixed structured messages are easier to review.
- Styles message headings in the stream, minimized notification, and detail view, and opens linked local files from the formatted detail.
- Adds a close shortcut to the message header.
- Makes web addresses and referenced local files clickable in formatted messages.
- Opens product-page links on the dedicated Voltura AI Watcher website.
- Lets you choose how long minimized notifications remain visible from the notification-area Settings menu.
- Makes Next open the newer visible message above the current one and Previous open the older message below it.
- Hides the minimized notification when the main message list is opened.

## v0.1.5

- Keeps Codex maximized when bringing its window to the foreground.
- Presents JSON messages as readable labeled summaries while preserving their original content when copied.

## v0.1.4

- Corrected .NET framework install logic.

## v0.1.3

- Corrected .NET framework install logic.

## v0.1.2

- Clears all visible rows for chats that have reached a resolved state, including earlier work updates from the same chat.

## v0.1.1

- Adds quick Open file, Copy file, and Copy path actions when a Codex message references an available local file.
- Polishes alignment and styling in the minimized notification and About menu.

## v0.1.0

- Introduces a compact Windows panel for following human-visible activity across local Codex chats.
- Highlights working, completed, approval, and input-needed states so important updates are easy to spot.
- Opens the related Codex chat directly and provides filters for focusing on the messages that need attention.
- Supports notification-area controls, optional message sounds, start-with-Windows, and a minimized startup mode.
- Plays the optional sound only when Codex is waiting for approval, input, or an app connection.
- Keeps cleared-message preferences locally without modifying Codex chat data.
