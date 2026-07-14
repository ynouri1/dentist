namespace Ortho.Domain.Entities;

public class Consultation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }

    public DateTime Date { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
