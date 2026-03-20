# Release Notes
## Added
- Undo and redo support for image replacement and restore actions.
- Keyboard shortcuts for undo and redo from the main editor window.
- Hierarchical TreeView explorer for browsing MCF images
- Folder-based navigation with icons and alphabetical sorting
- ImageIdMap support with configurable settings

## Improved
- Dirty state is now recalculated from modified image entries.
- Undo history is cleared when loading a new MCF project.
- Reduced the default startup window size.
- Preview now automatically scales down large images to fit the viewer.
- Small images are no longer upscaled in the preview.
- McfEditor now purges its temporary working directory on application exit.
- Temporary extraction files no longer accumulate in the user's temp folder.
- Temporary working data is now cleaned before opening a new MCF project.
- Dark theme styling for TreeView with hover and selection feedback
- Consistent UI layout with rounded panels and improved visual integration
- Restored scrollbar behavior in image explorer

## Fixed
- Fixed preview not resetting when selecting folders or no image
