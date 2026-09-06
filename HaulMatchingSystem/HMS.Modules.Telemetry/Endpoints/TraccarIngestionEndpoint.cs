using HMS.Shared.Core.Events;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace HMS.Modules.Telemetry.Endpoints
{
    public static class TraccarIngestionEndpoint
    {
        public static void MapTraccarEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/api/telemetry/osm", async (
                HttpContext context,
                [FromServices] IPublisher publisher,
                [FromServices] IConfiguration configuration,
                CancellationToken ct) =>
            {
                // Authenticated with a shared device token (header "X-Device-Token" or query "token").
                // Previously anonymous — anyone could inject fake GPS for any device.
                var expectedToken = configuration["Telemetry:DeviceToken"];
                if (string.IsNullOrEmpty(expectedToken))
                {
                    return Results.Problem("Telemetry device token is not configured.", statusCode: 503);
                }

                var providedToken = context.Request.Headers["X-Device-Token"].FirstOrDefault()
                    ?? context.Request.Query["token"].FirstOrDefault();
                if (string.IsNullOrEmpty(providedToken) ||
                    !string.Equals(providedToken, expectedToken, System.StringComparison.Ordinal))
                {
                    return Results.Unauthorized();
                }

                var req = context.Request;

                // Đọc toàn bộ chuỗi nằm trong Body của POST request
                using var reader = new StreamReader(req.Body);
                string body = await reader.ReadToEndAsync(ct);

                // Sử dụng HttpUtility để parse chuỗi "id=xxx&lat=yyy" thành NameValueCollection
                var parsedParams = System.Web.HttpUtility.ParseQueryString(body);

                // Lấy dữ liệu ra
                string id = parsedParams["id"] ?? parsedParams["deviceid"];
                string latStr = parsedParams["lat"];
                string lonStr = parsedParams["lon"];
                string speedStr = parsedParams["speed"];
                string battStr = parsedParams["batt"];

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(latStr) || string.IsNullOrEmpty(lonStr))
                {
                    return Results.BadRequest("Thiếu thông tin GPS");
                }

                // Parse explicitly and reject malformed numbers instead of silently storing (0,0).
                if (!decimal.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                    !decimal.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                {
                    return Results.BadRequest("Tọa độ GPS không hợp lệ.");
                }

                if (lat is < -90m or > 90m || lon is < -180m or > 180m || (lat == 0m && lon == 0m))
                {
                    return Results.BadRequest("Tọa độ GPS ngoài phạm vi cho phép.");
                }

                var gpsEvent = new GpsPingReceivedEvent
                {
                    DeviceId = id,
                    Lat = lat,
                    Lng = lon,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                await publisher.Publish(gpsEvent, ct);
                return Results.Ok();
            })
            .WithTags("Telemetry");
        }
    }
}