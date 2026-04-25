# Art Prompt Records

Store one prompt record per generated asset target. Use the same stem as the manifest `promptRef`.

Each prompt record should include:

- Manifest asset id
- Date generated
- Model/tool used
- Full prompt
- Negative prompt or exclusions
- Source/reference notes
- Output dimensions
- Selected output file path
- QA notes before runtime replacement

Do not replace runtime art only from memory. Add or update the manifest record and prompt record in the same change.
