using Microsoft.AspNetCore.Mvc.ApplicationModels;

public sealed class RouteConvention(string prefix) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        foreach (SelectorModel selector in controller.Selectors)
        {
            if (selector.AttributeRouteModel != null)
            {
                if (selector.AttributeRouteModel.Template.StartsWith("~/", StringComparison.Ordinal))
                {
                    selector.AttributeRouteModel.Template = selector.AttributeRouteModel.Template[2..];
                    continue;
                }

                selector.AttributeRouteModel.Template =
                    $"{prefix}/{selector.AttributeRouteModel.Template}";
            }
        }
    }
}
