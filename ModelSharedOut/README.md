# ModelSharedOut

This project generates client-facing contracts from the Well service and the external schemas required by its consumers.

## Current schema inputs

- `json-schemas/RigModel.json` provides Rig contract types used by the Well UI.
- `json-schemas/VerticalDatumModel.json` provides Vertical Datum contract types used for mean-sea-level references.

Regenerate the shared output after changing an input schema or the Well REST contract, then rebuild consumers to confirm compatibility.
