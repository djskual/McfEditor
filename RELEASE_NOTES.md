# Release Notes
## Added
- Native C# backend for MCF extraction, imageidmap parsing and rebuild
- PNG encoding/decoding pipeline for image processing
- Added a bottom progress bar with real-time updates during MCF extraction and rebuild operations.

## Improved
- McfEditor is now fully standalone and no longer requires Python
- Faster and more reliable MCF processing using native implementation
- Simplified settings by removing redundant ImageIdMap parsing option
- Preserved tree view selection after replacing, restoring or undoing image modifications.
- Simplified main window layout by removing the unused top toolbar row.
- Disabled main UI interactions during long-running tasks to prevent conflicting actions.

## Fixed
- Fixed tree view losing focus after image modifications.

## Removed
- Removed Python backend and all related scripts and dependencies
