using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Forms.Automate.Triggers;

namespace Umbraco.Forms.Automate;

/// <summary>
/// Registers the package's services with the Umbraco dependency injection container.
/// </summary>
public sealed class FormsAutomateComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddTransient<FormFieldResolver>();
}
