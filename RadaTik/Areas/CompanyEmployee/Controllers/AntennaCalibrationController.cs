using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.Calibration;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
public class AntennaCalibrationController : global::RadaTik.Controllers.AntennaCalibrationController
{
    public AntennaCalibrationController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILineOfSightAnalysisService lineOfSight,
        IAntennaCalibrationSessionStore sessions)
        : base(context, userManager, lineOfSight, sessions)
    {
    }
}
