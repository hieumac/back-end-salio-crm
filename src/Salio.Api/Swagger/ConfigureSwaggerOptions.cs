using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Salio.Api.Swagger;

/// <summary>
/// Sinh <see cref="OpenApiInfo"/> cho từng API version mà <see cref="IApiVersionDescriptionProvider"/>
/// khám phá được, để Swagger hiển thị tất cả các version (v1, v2, …) một cách tự động.
/// </summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) => _provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "Salio Sales AI API",
            Version = description.ApiVersion.ToString(),
            Description = "REST API cho hệ thống Salio CRM — quản lý leads, deals, contacts, tasks, pipelines, sản phẩm và người dùng.",
            Contact = new OpenApiContact
            {
                Name = "Salio Team",
                Email = "support@ezcloud.vn",
            },
            License = new OpenApiLicense
            {
                Name = "Proprietary — © ezCloud",
            },
        };

        if (description.IsDeprecated)
        {
            info.Description += " ⚠️ Phiên bản này đã bị deprecated, vui lòng dùng version mới hơn.";
        }

        return info;
    }
}
