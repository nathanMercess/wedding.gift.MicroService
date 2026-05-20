using Microsoft.AspNetCore.Mvc.ApplicationModels;

public class RouteConvention(string prefix) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        foreach (var selector in controller.Selectors)
        {
            if (selector.AttributeRouteModel != null)
            {
                selector.AttributeRouteModel.Template =
                    $"{prefix}/{selector.AttributeRouteModel.Template}";
            }
        }
    }
}