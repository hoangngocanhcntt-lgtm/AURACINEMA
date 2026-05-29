using AuraCinema.Domain.Models.Chat;

namespace AuraCinema.Services.Chat.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, IChatTool> _tools;

    public ToolRegistry(IEnumerable<IChatTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public IChatTool? Get(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public List<LlmTool> GetAllDeclarations()
    {
        return _tools.Values.Select(t => new LlmTool
        {
            Function = new LlmFunctionDeclaration
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Schema
            }
        }).ToList();
    }
}
