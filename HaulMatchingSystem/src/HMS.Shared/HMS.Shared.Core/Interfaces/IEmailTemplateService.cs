namespace HMS.Shared.Core.Interfaces;

/// <summary>
/// Generates HTML email bodies for HMS notifications.
/// All templates are UTF-8 safe and support Vietnamese characters.
/// </summary>
public interface IEmailTemplateService
{
    string IncidentReported(string incidentCode, string tripCode, string driverName, string vehicle,
        string route, string incidentType, string description, int evidenceCount, DateTimeOffset reportedAt);

    string IncidentInProgress(string incidentCode, string tripCode, string incidentType,
        string staffName, DateTimeOffset assignedAt);

    string IncidentResolved(string incidentCode, string tripCode, string incidentType,
        string resolutionNote, string resolvedBy, DateTimeOffset resolvedAt);

    string IncidentRejected(string incidentCode, string tripCode, string incidentType,
        string rejectReason, string actorName, DateTimeOffset timestamp);
}
