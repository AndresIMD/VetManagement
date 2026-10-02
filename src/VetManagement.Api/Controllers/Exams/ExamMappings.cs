using VetManagement.Contracts.Exams;
using VetManagement.Domain.Exams;

namespace VetManagement.Api.Controllers.Exams;

/// <summary>
/// Request -> Domain and Domain -> DTO mapping for the exam controllers.
/// Ids of the root entity come from the route, never from the body.
/// </summary>
internal static class ExamMappings
{
    public static ExamDto ToDto(this Exam exam) => new()
    {
        Id = exam.Id,
        Name = exam.Name,
        Brand = exam.Brand,
        Machine = exam.Machine,
        SampleType = exam.SampleType,
        SampleContainer = exam.SampleContainer,
        BuyPrice = exam.BuyPrice,
        SellPrice = exam.SellPrice
    };

    public static Exam ToDomain(this ExamRequest request, int id) => new()
    {
        Id = id,
        Name = request.Name,
        Brand = request.Brand,
        Machine = request.Machine,
        SampleType = request.SampleType,
        SampleContainer = request.SampleContainer,
        BuyPrice = request.BuyPrice,
        SellPrice = request.SellPrice
    };

    public static ExternalLabDto ToDto(this ExternalLab lab) => new()
    {
        Id = lab.Id,
        Name = lab.Name,
        ContactName = lab.ContactName,
        Phone = lab.Phone,
        Email = lab.Email,
        Address = lab.Address,
        Notes = lab.Notes,
        DefaultTurnaroundDays = lab.DefaultTurnaroundDays
    };

    public static ExternalLab ToDomain(this ExternalLabRequest request, int id) => new()
    {
        Id = id,
        Name = request.Name,
        ContactName = request.ContactName,
        Phone = request.Phone,
        Email = request.Email,
        Address = request.Address,
        Notes = request.Notes,
        DefaultTurnaroundDays = request.DefaultTurnaroundDays
    };

    public static ExamPerformedDto ToDto(this ExamPerformed exam) => new()
    {
        Id = exam.Id,
        PatientId = exam.PatientId,
        Responsible = exam.Responsible,
        Date = exam.Date,
        Items = exam.Items.Select(i => new ExamRequestItemDto
        {
            Id = i.Id,
            ExamId = i.ExamId,
            Status = i.Status,
            IsExternal = i.IsExternal,
            ExternalEstimatedDays = i.ExternalEstimatedDays,
            ExternalLabId = i.ExternalLabId,
            ExternalLab = i.ExternalLab?.ToDto(),
            Attributes = i.Attributes
        }).ToList()
    };

    // Items keep their ids so an update modifies existing rows (0 = new item).
    // ExternalLab is linked by id only, so an order can never create or overwrite a lab.
    public static ExamPerformed ToDomain(this ExamPerformedRequest request, int id) => new()
    {
        Id = id,
        PatientId = request.PatientId,
        Responsible = request.Responsible,
        Date = request.Date,
        Items = request.Items.Select(i => new ExamRequestItem
        {
            Id = i.Id,
            ExamId = i.ExamId,
            Status = i.Status,
            IsExternal = i.IsExternal,
            ExternalEstimatedDays = i.ExternalEstimatedDays,
            ExternalLabId = i.ExternalLabId,
            Attributes = i.Attributes
        }).ToList()
    };
}
