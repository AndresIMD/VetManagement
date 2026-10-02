using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Configuration;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Infrastructure.Repositories;

public class ClinicSettingRepository(AppDbContext context) : Repository<ClinicSetting>(context), IClinicSettingRepository
{
    public async Task<ClinicSetting?> GetByKeyAsync(string key)
        => await _context.ClinicSettings.FirstOrDefaultAsync(s => s.Key == key);
}
