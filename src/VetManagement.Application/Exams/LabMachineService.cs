using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Localization;

namespace VetManagement.Application.Exams;

public enum LabMachineStatus
{
    Done,
    NotFound,
    Invalid
}

/// <param name="ExamsUpdated">Catalog exams that took the new brand or model after a rename.</param>
public sealed record LabMachineResult(LabMachineStatus Status, int? Id = null, string? Error = null, int ExamsUpdated = 0);

/// <summary>
/// The brands and models of lab machines the exam catalog picks from. Exams keep the brand and model as text, so a
/// rename is carried over to the exams that use the machine, and a machine still in use can't be deleted.
/// Issued reports are not touched: they printed the method of their day.
/// </summary>
public class LabMachineService(IUnitOfWork unitOfWork, AuditService audit)
{
    public async Task<List<(LabMachine Machine, int ExamCount)>> ListAsync()
    {
        var machines = await unitOfWork.LabMachines.GetAllAsync();
        var exams = await unitOfWork.Exams.GetAllAsync();
        return [.. machines
            .OrderBy(m => m.Brand, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.Model, StringComparer.CurrentCultureIgnoreCase)
            .Select(m => (m, exams.Count(e => Uses(e, m.Brand, m.Model))))];
    }

    public async Task<LabMachineResult> AddAsync(string brand, string model, string userName)
    {
        (brand, model) = (brand.Trim(), model.Trim());
        if (await ValidateAsync(brand, model, exceptId: null) is { } error)
            return new(LabMachineStatus.Invalid, Error: error);

        var machine = new LabMachine { Brand = brand, Model = model };
        await unitOfWork.LabMachines.AddAsync(machine);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(LabMachine), machine.Id, AuditActionType.Add, JsonSerializer.Serialize(machine), userName);
        return new(LabMachineStatus.Done, machine.Id);
    }

    public async Task<LabMachineResult> UpdateAsync(int id, string brand, string model, string userName)
    {
        var machine = await unitOfWork.LabMachines.GetByIdAsync(id);
        if (machine is null)
            return new(LabMachineStatus.NotFound);
        (brand, model) = (brand.Trim(), model.Trim());
        if (await ValidateAsync(brand, model, exceptId: id) is { } error)
            return new(LabMachineStatus.Invalid, Error: error);

        var before = JsonSerializer.Serialize(machine);
        var exams = (await unitOfWork.Exams.GetAllAsync()).Where(e => Uses(e, machine.Brand, machine.Model)).ToList();
        foreach (var exam in exams)
        {
            exam.Brand = brand;
            exam.Machine = model;
            unitOfWork.Exams.Update(exam);
        }
        machine.Brand = brand;
        machine.Model = model;
        unitOfWork.LabMachines.Update(machine);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(LabMachine), id, AuditActionType.Edit,
            $"Before: {before}\nAfter: {JsonSerializer.Serialize(machine)}\nExams updated: {string.Join(", ", exams.Select(e => e.Id))}", userName);
        return new(LabMachineStatus.Done, id, ExamsUpdated: exams.Count);
    }

    public async Task<LabMachineResult> DeleteAsync(int id, string userName)
    {
        var machine = await unitOfWork.LabMachines.GetByIdAsync(id);
        if (machine is null)
            return new(LabMachineStatus.NotFound);
        var inUse = (await unitOfWork.Exams.GetAllAsync()).Count(e => Uses(e, machine.Brand, machine.Model));
        if (inUse > 0)
            return new(LabMachineStatus.Invalid, Error: Texts.T("{0} catalog exams run on this machine. Move them to another machine first.", inUse));

        var snapshot = JsonSerializer.Serialize(machine);
        unitOfWork.LabMachines.Remove(machine);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(LabMachine), id, AuditActionType.Delete, snapshot, userName);
        return new(LabMachineStatus.Done, id);
    }

    private async Task<string?> ValidateAsync(string brand, string model, int? exceptId)
    {
        if (brand.Length == 0 || model.Length == 0)
            return Texts.T("Enter the brand and the model.");
        var machines = await unitOfWork.LabMachines.GetAllAsync();
        return machines.Any(m => m.Id != exceptId && Same(m.Brand, brand) && Same(m.Model, model))
            ? Texts.T("'{0} {1}' is already on the list.", brand, model)
            : null;
    }

    private static bool Uses(Exam exam, string brand, string model) => Same(exam.Brand, brand) && Same(exam.Machine, model);

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
