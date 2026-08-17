using HMS.Shared.Core.Interfaces;

namespace HMS.Shared.Infrastructure.Services;

/// <summary>
/// HTML email template service for HMS notifications.
/// All templates use UTF-8, basic responsive CSS, no JavaScript.
/// </summary>
public sealed class EmailTemplateService : IEmailTemplateService
{
    private static string Layout(string title, string content)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>{title}</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f5f7;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f5f7;padding:24px 0;">
            <tr>
              <td align="center">
                <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.08);">
                  <!-- Header -->
                  <tr>
                    <td style="background:linear-gradient(135deg,#1a73e8,#0d47a1);padding:24px 32px;">
                      <h1 style="margin:0;color:#ffffff;font-size:20px;font-weight:600;">
                        🚛 {title}
                      </h1>
                    </td>
                  </tr>
                  <!-- Body -->
                  <tr>
                    <td style="padding:32px;">
                      {content}
                    </td>
                  </tr>
                  <!-- Footer -->
                  <tr>
                    <td style="background:#f8f9fa;padding:20px 32px;border-top:1px solid #e8eaed;">
                      <p style="margin:0;font-size:12px;color:#5f6368;text-align:center;">
                        HMS - Hub Management System<br>
                        Vui lòng không trả lời email này.
                      </p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string InfoRow(string label, string value)
    {
        return $"""
        <tr>
          <td style="padding:8px 0;font-size:14px;color:#5f6368;white-space:nowrap;width:160px;vertical-align:top;">{label}</td>
          <td style="padding:8px 0;font-size:14px;color:#202124;font-weight:500;">{value}</td>
        </tr>
        """;
    }

    private static string StatusBadge(string text, string color)
    {
        return $"""<span style="display:inline-block;padding:4px 12px;border-radius:20px;font-size:13px;font-weight:600;color:#ffffff;background:{color};">{text}</span>""";
    }

    public string IncidentReported(string incidentCode, string tripCode, string driverName, string vehicle,
        string route, string incidentType, string description, int evidenceCount, DateTimeOffset reportedAt)
    {
        var vietnameseType = MapIncidentType(incidentType);
        var content = $"""
        <p style="margin:0 0 20px 0;font-size:15px;color:#202124;">
          Một sự cố mới đã được tài xế <strong>{driverName}</strong> báo cáo.
        </p>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px;">
          {InfoRow("Mã sự cố:", incidentCode)}
          {InfoRow("Chuyến:", tripCode)}
          {InfoRow("Tài xế:", driverName)}
          {InfoRow("Xe:", vehicle)}
          {InfoRow("Tuyến:", route)}
          {InfoRow("Loại sự cố:", vietnameseType)}
          {InfoRow("Mô tả:", description)}
          {InfoRow("Số ảnh bằng chứng:", evidenceCount.ToString())}
          {InfoRow("Thời gian:", reportedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))}
          {InfoRow("Trạng thái:", StatusBadge("Open", "#1a73e8"))}
        </table>
        <p style="margin:0;font-size:14px;color:#5f6368;">
          Vui lòng đăng nhập HMS để xem chi tiết và xử lý sự cố.
        </p>
        """;
        return Layout("BÁO CÁO SỰ CỐ MỚI", content);
    }

    public string IncidentInProgress(string incidentCode, string tripCode, string incidentType,
        string staffName, DateTimeOffset assignedAt)
    {
        var vietnameseType = MapIncidentType(incidentType);
        var content = $"""
        <p style="margin:0 0 20px 0;font-size:15px;color:#202124;">
          Sự cố của chuyến <strong>{tripCode}</strong> đã được tiếp nhận xử lý.
        </p>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px;">
          {InfoRow("Mã sự cố:", incidentCode)}
          {InfoRow("Loại:", vietnameseType)}
          {InfoRow("Trạng thái:", StatusBadge("Đang xử lý", "#f59e0b"))}
          {InfoRow("Người tiếp nhận:", staffName)}
          {InfoRow("Thời gian:", assignedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))}
        </table>
        <p style="margin:0;font-size:14px;color:#5f6368;">
          Vui lòng đăng nhập HMS để theo dõi sự cố.
        </p>
        """;
        return Layout("SỰ CỐ ĐANG ĐƯỢC XỬ LÝ", content);
    }

    public string IncidentResolved(string incidentCode, string tripCode, string incidentType,
        string resolutionNote, string resolvedBy, DateTimeOffset resolvedAt)
    {
        var vietnameseType = MapIncidentType(incidentType);
        var content = $"""
        <p style="margin:0 0 20px 0;font-size:15px;color:#202124;">
          Sự cố <strong>{incidentCode}</strong> đã được xử lý thành công.
        </p>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px;">
          {InfoRow("Mã sự cố:", incidentCode)}
          {InfoRow("Chuyến:", tripCode)}
          {InfoRow("Loại:", vietnameseType)}
          {InfoRow("Trạng thái:", StatusBadge("Đã xử lý", "#059669"))}
          {InfoRow("Kết quả xử lý:", resolutionNote)}
          {InfoRow("Xử lý bởi:", resolvedBy)}
          {InfoRow("Thời gian:", resolvedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))}
        </table>
        <p style="margin:0;font-size:14px;color:#5f6368;">
          Vui lòng đăng nhập HMS để xem chi tiết.
        </p>
        """;
        return Layout("SỰ CỐ ĐÃ ĐƯỢC XỬ LÝ", content);
    }

    public string IncidentRejected(string incidentCode, string tripCode, string incidentType,
        string rejectReason, string actorName, DateTimeOffset timestamp)
    {
        var vietnameseType = MapIncidentType(incidentType);
        var content = $"""
        <p style="margin:0 0 20px 0;font-size:15px;color:#202124;">
          Báo cáo sự cố <strong>{incidentCode}</strong> không được tiếp nhận.
        </p>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px;">
          {InfoRow("Mã sự cố:", incidentCode)}
          {InfoRow("Chuyến:", tripCode)}
          {InfoRow("Loại:", vietnameseType)}
          {InfoRow("Trạng thái:", StatusBadge("Rejected", "#dc2626"))}
          {InfoRow("Lý do:", rejectReason)}
          {InfoRow("Xử lý bởi:", actorName)}
          {InfoRow("Thời gian:", timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))}
        </table>
        <p style="margin:0;font-size:14px;color:#5f6368;">
          Vui lòng đăng nhập HMS để xem chi tiết.
        </p>
        """;
        return Layout("BÁO CÁO SỰ CỐ KHÔNG ĐƯỢC TIẾP NHẬN", content);
    }

    private static string MapIncidentType(string type) => type switch
    {
        "Delay" => "Trễ hạn",
        "VehicleBreakdown" => "Hỏng xe",
        "Accident" => "Tai nạn",
        "CargoDamage" => "Hư hỏng hàng hóa",
        "CargoLost" => "Mất hàng",
        "DeliveryProblem" => "Sự cố giao hàng",
        "RouteProblem" => "Sự cố tuyến đường",
        "Weather" => "Thời tiết",
        "Other" => "Khác",
        _ => type
    };
}
