using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.RegisterSessions;
using DomainRegisterSessionStatus = Pos.Domain.RegisterSessions.RegisterSessionStatus;

namespace Pos.Application.Tests.RegisterSessions;

internal sealed class FakeRegisterSessionRepository : IRegisterSessionRepository
{
    private readonly List<RegisterSession> _sessions;

    public FakeRegisterSessionRepository(IEnumerable<RegisterSession>? seed = null)
    {
        _sessions = seed?.ToList() ?? [];
    }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    // Punto de extensión para forzar la detección defensiva de más de una sesión abierta por
    // Register, tal como hace EfRegisterSessionRepository.GetOpenByRegisterAsync.
    public bool ThrowOnMultipleOpenForRegister { get; set; } = true;

    public Task<RegisterSession?> GetByIdAsync(RegisterSessionId registerSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.SingleOrDefault(s => s.Id == registerSessionId));

    public Task<RegisterSession?> GetOpenByRegisterAsync(RegisterId registerId, CancellationToken cancellationToken)
    {
        var openSessions = _sessions
            .Where(s => s.RegisterId == registerId && s.Status == DomainRegisterSessionStatus.Open)
            .ToList();

        if (openSessions.Count > 1 && ThrowOnMultipleOpenForRegister)
        {
            throw new InvalidOperationException($"El Register '{registerId}' tiene más de una RegisterSession abierta.");
        }

        return Task.FromResult(openSessions.FirstOrDefault());
    }

    public Task AddAsync(RegisterSession registerSession, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _sessions.Add(registerSession);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(RegisterSession registerSession, CancellationToken cancellationToken)
    {
        UpdateCallCount++;

        return Task.CompletedTask;
    }
}
