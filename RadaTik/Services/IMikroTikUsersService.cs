using RadaTik.Services.MikroTik;

namespace RadaTik.Services;

/// <summary>واجهة مجمّعة لتوافق الكود الحالي — يُفضّل الاعتماد على الواجهات الضيقة.</summary>
public interface IMikroTikUsersService : IMikroTikUserImportService, IMikroTikPppoeUserService;
