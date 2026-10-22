@page "/services"

<PageTitle>Services - Hexalith EventStore</PageTitle>

<h1 style="font-size: 24px; margin: 0 0 16px;">Services</h1>

<EmptyState Title="EventStore is running. 0 domain services connected."
            Description="Connected domain services will appear here once they register with the EventStore."
            Icon="@ServicesIcon"
            ActionLabel="Read the domain service registration guide"
            ActionHref="https://github.com/Hexalith/Hexalith.EventStore/blob/main/docs/getting-started/first-domain-service.md" />

@code {
    private static RenderFragment ServicesIcon => builder => builder.AddMarkupContent(0, "&#128187;");
}
