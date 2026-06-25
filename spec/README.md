# OpenAI OpenAPI Spec

`openai-openapi.yaml` is a pinned snapshot of the official OpenAI OpenAPI specification.

- **Source:** https://github.com/openai/openai-openapi
- **Pinned commit:** `5162af98d3147432c14680df789e8e12d4891e6b`

Run `make update-spec` to pull the latest version and update this file with the new commit SHA.

The spec drives Kiota client generation (scoped to `/v1/chat/completions`, `/v1/responses`, `/v1/models`)
and is the authoritative reference for spec-compliance tests in `tests/Katasec.OaiServer.Tests/`.
