using HMS.Shared.Core.Events;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
                CancellationToken ct) =>
            {
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

                Console.WriteLine($"\n[DEBUG] 🚀 RAW BODY: {body}");
                Console.WriteLine($"[DEBUG] ĐÃ PARSE -> ID: '{id}' | Lat: '{latStr}' | Lon: '{lonStr}'\n");

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(latStr) || string.IsNullOrEmpty(lonStr))
                {
                    return Results.BadRequest("Thiếu thông tin GPS");
                }

                // Ép kiểu an toàn bằng InvariantCulture
                decimal.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lat);
                decimal.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lon);

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