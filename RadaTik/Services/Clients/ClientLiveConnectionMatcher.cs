namespace RadaTik.Services.Clients;

/// <summary>
/// يطابق مشتركي قاعدة البيانات مع جلسات MikroTik <c>/ppp/active</c>.
/// </summary>
public static class ClientLiveConnectionMatcher
{
    public static string NormalizeUserName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string name = value.Trim();
        if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
        {
            name = name[1..^1].Trim();
        }

        return name;
    }

    /// <summary>
    /// يُفضَّل المطابقة على نفس السيرفر + اسم المستخدم.
    /// إذا لم يوجد مشترك على ذلك السيرفر، يُطابق أي مشترك بنفس الاسم داخل الشركة
    /// (نسخ سرّ PPP بدون صف محلي على البرج الهدف).
    /// لا يشترط أن يكون الحساب نشطاً في قاعدة البيانات.
    /// </summary>
    public static HashSet<int> Match(
        IReadOnlyList<Models.Client> clients,
        IReadOnlyDictionary<int, IReadOnlyCollection<string>> activeNamesByServer)
    {
        HashSet<int> connectedIds = [];
        List<Models.Client> namedClients = clients
            .Where(c => !string.IsNullOrWhiteSpace(c.UserName))
            .ToList();

        foreach ((int serverId, IReadOnlyCollection<string> sessionNames) in activeNamesByServer)
        {
            foreach (string rawName in sessionNames)
            {
                string sessionName = NormalizeUserName(rawName);
                if (sessionName.Length == 0)
                {
                    continue;
                }

                List<Models.Client> onServer = namedClients
                    .Where(c =>
                        c.MikroTikServerId == serverId
                        && string.Equals(
                            NormalizeUserName(c.UserName),
                            sessionName,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (onServer.Count > 0)
                {
                    foreach (Models.Client client in onServer)
                    {
                        connectedIds.Add(client.Id);
                    }

                    continue;
                }

                foreach (Models.Client client in namedClients)
                {
                    if (string.Equals(
                            NormalizeUserName(client.UserName),
                            sessionName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        connectedIds.Add(client.Id);
                    }
                }
            }
        }

        return connectedIds;
    }
}
