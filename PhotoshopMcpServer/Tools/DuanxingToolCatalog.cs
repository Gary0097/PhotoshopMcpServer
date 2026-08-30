namespace PhotoshopMcpServer.Tools;

public static class DuanxingToolCatalog
{
    public static IReadOnlyList<Type> GetToolTypes(bool includeDeveloperTools)
    {
        var toolTypes = new List<Type>
        {
            typeof(EnvironmentTools),
            typeof(DuanxingWorkflowTools),
            typeof(DuanxingQuickActionTools),
            typeof(PhotoshopProductionTools),
            typeof(IllustratorProductionTools)
        };
        if (includeDeveloperTools)
        {
            toolTypes.Add(typeof(PhotoshopTools));
            toolTypes.Add(typeof(IllustratorTools));
        }
        return toolTypes;
    }
}
