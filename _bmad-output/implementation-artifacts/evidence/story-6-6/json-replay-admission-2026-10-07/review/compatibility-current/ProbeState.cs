public sealed class ProbeState { public int Total {get; private set;} public void Apply(ProbeEvent e) { Total += e.Amount; } }
