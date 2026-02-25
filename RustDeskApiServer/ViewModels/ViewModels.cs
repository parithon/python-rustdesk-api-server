namespace RustDeskApiServer.ViewModels;

public record LoginRequest(
    string Username,
    string Password,
    string Id,
    string Uuid,
    bool AutoLogin,
    string Type,
    object? DeviceInfo
);

public record LoginResponse(
    string? AccessToken,
    string? Type,
    UserInfo? User,
    string? Error
);

public record UserInfo(string Name);

public record CurrentUserResponse(
    string? AccessToken,
    string? Type,
    string? Name
);

public record AbData(
    string[] Tags,
    PeerData[] Peers,
    string TagColors
);

public record PeerData(
    string Id,
    string Username,
    string Hostname,
    string Alias,
    string Platform,
    string[] Tags,
    string Hash
);

public record AbRequest(string? Data);

public record SysInfoRequest(
    string Id,
    string Cpu,
    string Hostname,
    string Memory,
    string Os,
    string? Username,
    string Uuid,
    string Version
);

public record HeartbeatRequest(string Id, string Uuid);

public record AuditRequest(
    string? Action,
    string? ConnId,
    string? Ip,
    string? Id,
    long? SessionId,
    string? Uuid,
    string? Peer,
    bool? IsFile,
    string? Path,
    string? PeerId,
    string? Info,
    int? Type
);

public record RegisterRequest(string User, string Pwd);

public record ShareRequest(string? Data);

public record SharePeerItem(string Value, string Title);

public record DeviceViewModel(
    string RustDeskId,
    string Version,
    string HasRHash,
    string Username,
    string Hostname,
    string Alias,
    string Platform,
    string Os,
    string Cpu,
    string Memory,
    string IpAddress,
    string CreateTime,
    string UpdateTime,
    string Status,
    string RustUser = ""
);

public record ConnLogViewModel(
    string FromIp,
    string FromId,
    string FromAlias,
    string RustDeskId,
    string Alias,
    string? ConnStart,
    string? ConnEnd,
    string Duration
);

public record FileLogViewModel(
    string File,
    string RemoteId,
    string RemoteAlias,
    string UserId,
    string UserAlias,
    string UserIp,
    string FileSize,
    int Direction,
    string? LoggedAt
);

public record ShareLinkViewModel(
    string SHash,
    bool IsUsed,
    bool IsExpired,
    string CreateTime,
    string Peers
);

public record WorkViewModel(
    Models.UserProfile User,
    bool ShowAll,
    IEnumerable<DeviceViewModel> Items,
    int PageNumber,
    int TotalPages
);

public record ConnLogPageViewModel(
    Models.UserProfile User,
    IEnumerable<ConnLogViewModel> Items,
    int PageNumber,
    int TotalPages
);

public record FileLogPageViewModel(
    Models.UserProfile User,
    IEnumerable<FileLogViewModel> Items,
    int PageNumber,
    int TotalPages
);

public record ShareViewModel(
    Models.UserProfile User,
    IEnumerable<SharePeerItem> Peers,
    IEnumerable<ShareLinkViewModel> ShareLinks
);
