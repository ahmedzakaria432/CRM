using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Crm.BuildingBlocks.Tests.Support;

public static class ConventionFixtures
{
    /// <summary>A controller model with the given area route value, N actions and M selectors per action. Built
    /// by hand: the conventions work on the application model, so no MVC host is needed.</summary>
    public static ControllerModel Controller(string? area, int actions, int selectorsPerAction)
    {
        var controller = new ControllerModel(typeof(FixtureController).GetTypeInfo(), []);

        if (area is not null)
        {
            controller.RouteValues["area"] = area;
        }

        MethodInfo method = typeof(FixtureController).GetMethod(nameof(FixtureController.Get))
            ?? throw new InvalidOperationException("fixture action missing");

        for (var i = 0; i < actions; i++)
        {
            var action = new ActionModel(method, []) { Controller = controller };
            for (var j = 0; j < selectorsPerAction; j++)
            {
                action.Selectors.Add(new SelectorModel());
            }

            controller.Actions.Add(action);
        }

        return controller;
    }

    public sealed class FixtureController
    {
        public static int Get() => 1;
    }
}
