using VetManagement.Domain.Configuration;

namespace VetManagement.Application.Contracts.Persistence;

public interface IClinicSettingRepository : IRepository<ClinicSetting>
{
    Task<ClinicSetting?> GetByKeyAsync(string key);
}
