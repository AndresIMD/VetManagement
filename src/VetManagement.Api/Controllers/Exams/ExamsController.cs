using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Authorization;
using VetManagement.Application.Inventory;
using VetManagement.Application.Services;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Exams;

[Route("api/[controller]")]
[Authorize(Policy = "Exams.Read")]
public class ExamsController(ExamService service, EnabledModules modules, WarehouseService warehouses) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ExamDto>>> GetAllAsync()
    {
        var exams = await service.GetAllAsync();
        var names = await ItemNamesAsync(exams);
        return Ok(exams.Select(e => e.ToDto(names)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var exam = await service.GetByIdAsync(id);
        if (exam == null)
            return NotFound();
        return Ok(exam.ToDto(await ItemNamesAsync([exam])));
    }

    /// <summary>
    /// Inventory products to link to an exam (name, brand or barcode contains the text), with their units in the daily-use
    /// warehouse, where the reports take them from.
    /// </summary>
    [HttpGet("stock-items")]
    [Authorize(Policy = "Exams.Update")]
    [RequiresModule(AppModule.Inventory)]
    public async Task<ActionResult<List<ExamStockItemDto>>> StockItemsAsync([FromQuery] string? search)
    {
        var items = await service.SearchStockItemsAsync(search);
        var views = await warehouses.ViewAsync(items, GetStockScope());
        return Ok(items.Select(i => new ExamStockItemDto { Id = i.Id, Name = i.Name, Stock = views[i.Id].InDailyUse }).ToList());
    }

    [HttpPost]
    [Authorize(Policy = "Exams.Create")]
    public async Task<IActionResult> AddAsync([FromBody] ExamRequest request)
    {
        var exam = request.ToDomain(id: 0);
        if (await CheckStockItemAsync(exam, previous: null) is { } problem)
            return problem;
        await service.AddAsync(exam, GetUserName());
        return Ok();
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Exams.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] ExamRequest request)
    {
        var oldData = await service.GetByIdAsNoTrackingAsync(id);
        if (oldData == null)
            return NotFound();

        var exam = request.ToDomain(id);
        if (await CheckStockItemAsync(exam, oldData) is { } problem)
            return problem;
        await service.UpdateAsync(exam, oldData, GetUserName());
        return Ok();
    }

    /// <summary>
    /// The linked inventory products must exist, and a species' own product names a species, once. With the Inventory
    /// module off the form doesn't show the links, so the exam keeps the ones it had.
    /// </summary>
    private async Task<IActionResult?> CheckStockItemAsync(Exam exam, Exam? previous)
    {
        if (!modules.IsEnabled(AppModule.Inventory))
        {
            (exam.ItemId, exam.ItemQuantity, exam.SpeciesItems) = previous is null
                ? (null, 1, [])
                : (previous.ItemId, previous.ItemQuantity, previous.SpeciesItems);
            return null;
        }
        var names = await service.StockItemNamesAsync([exam]);
        (string Field, string Message)? problem =
            exam.ItemId is int itemId && !names.ContainsKey(itemId) ? (nameof(ExamRequest.ItemId), Texts.T("The inventory item doesn't exist."))
            : exam.SpeciesItems.Any(s => s.Species == Species.Unknown) ? (nameof(ExamRequest.SpeciesItems), Texts.T("Choose the species of each species' product."))
            : exam.SpeciesItems.GroupBy(s => s.Species).Any(g => g.Count() > 1) ? (nameof(ExamRequest.SpeciesItems), Texts.T("Each species can have only one product of its own."))
            : exam.SpeciesItems.Any(s => !names.ContainsKey(s.ItemId)) ? (nameof(ExamRequest.SpeciesItems), Texts.T("The inventory item doesn't exist."))
            : null;
        return problem is { } p
            ? ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [p.Field] = [p.Message] }))
            : null;
    }

    /// <summary>Names of the linked products; null with the Inventory module off, so no link is shown.</summary>
    private async Task<Dictionary<int, string>?> ItemNamesAsync(IEnumerable<Exam> exams)
        => modules.IsEnabled(AppModule.Inventory) ? await service.StockItemNamesAsync(exams) : null;

    [HttpDelete("{id}")]
    [Authorize(Policy = "Exams.Delete")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var result = await service.DeleteAsync(id, GetUserName());
        if (!result)
            return NotFound();
        return Ok();
    }
}
