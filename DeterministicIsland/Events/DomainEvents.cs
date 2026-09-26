using DeterministicIsland.Audit;
using DeterministicIsland.domain;

namespace DeterministicIsland.Events
{
    public interface IDomainEvent
    {
        DateTime OccurredAt { get; }
    }

    // §22.7: a new version of a vault fact was registered: a value, a pointer or a
    // derived island (including an automatic recomputation).
    public sealed record IslandAdded(
        string Hash,
        string Query,
        ILMVector Vector,
        string? DerivationRule,
        IReadOnlyList<string>? DerivationInputs,
        DateTime OccurredAt) : IDomainEvent;

    // §22.7: a vault fact governed a decision: it backed an island that was triggered in
    // a control cycle, or it answered a factual query through a Static Vault hit.
    public sealed record IslandTriggered(
        string Hash,
        string Query,
        Guid VectorId,
        string Version,
        DateTime OccurredAt) : IDomainEvent;

    // One per control cycle; the audit trail is a listener on this event (§23.3.2).
    public sealed record ControlDecisionMade(AuditRecord Record) : IDomainEvent
    {
        public DateTime OccurredAt => Record.Timestamp;
    }

    // §23.4 Observer: listeners react to an event without its source knowing they exist.
    public interface IDomainEventListener<in TEvent> where TEvent : IDomainEvent
    {
        void Handle(TEvent domainEvent);
    }

    public sealed class DomainEventBus
    {
        private readonly List<(Type EventType, Action<IDomainEvent> Handler)> _handlers = new();

        public DomainEventBus Subscribe<TEvent>(IDomainEventListener<TEvent> listener) where TEvent : IDomainEvent
        {
            _handlers.Add((typeof(TEvent), e => listener.Handle((TEvent)e)));
            return this;
        }

        public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            foreach (var (eventType, handler) in _handlers)
            {
                if (eventType.IsInstanceOfType(domainEvent))
                    handler(domainEvent);
            }
        }
    }
}
