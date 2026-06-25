SPEC_URL     := https://raw.githubusercontent.com/openai/openai-openapi/master/openapi.yaml
SPEC_FILE    := spec/openai-openapi.yaml
SPEC_SHA_URL := https://api.github.com/repos/openai/openai-openapi/commits?per_page=1

.PHONY: update-spec test

update-spec:
	@echo "Downloading OpenAI OpenAPI spec..."
	@mkdir -p spec
	@curl -sL $(SPEC_URL) -o $(SPEC_FILE)
	@SHA=$$(curl -sL "$(SPEC_SHA_URL)" | grep '"sha"' | head -1 | cut -d'"' -f4); \
	printf "# OpenAI OpenAPI Spec\n\n\`openai-openapi.yaml\` is a pinned snapshot of the official OpenAI OpenAPI specification.\n\n- **Source:** https://github.com/openai/openai-openapi\n- **Pinned commit:** \`$$SHA\`\n\nRun \`make update-spec\` to pull the latest version and update this file with the new commit SHA.\n\nThe spec is the authoritative reference for spec-compliance tests in \`tests/Katasec.OaiServer.Tests/\`.\nTests use the official \`OpenAI\` NuGet SDK as the test client — no code generation needed.\n" > spec/README.md
	@echo "Spec pinned at $$(grep 'Pinned commit' spec/README.md | cut -d'\`' -f2)"

test:
	@dotnet test tests/Katasec.OaiServer.Tests/Katasec.OaiServer.Tests.csproj
