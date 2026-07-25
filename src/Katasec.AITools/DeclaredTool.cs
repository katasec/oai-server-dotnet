using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Katasec.AITools;

// Declaration-only AIFunction: carries a name/description/schema to a provider verbatim.
// Never invoked — the caller (a wire relay, or an in-process agentic loop) executes the real
// tool itself and never calls AIFunction.InvokeAsync on this. Fails loud, not silently, if
// something ever tries to invoke it directly.
public sealed class DeclaredTool(string name, string description, JsonElement schema) : AIFunction
{
    public override string Name => name;
    public override string Description => description;
    public override JsonElement JsonSchema => schema;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
        => throw new NotSupportedException(
            $"'{name}' is declaration-only — it is never invoked directly. " +
            "The caller executes the real tool and never calls AIFunction.InvokeAsync.");
}
