@using Microsoft.AspNetCore.Components.Web

<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <HeadOutlet @rendermode="RenderMode.InteractiveServer" />
    <link href="_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css" rel="stylesheet" />
</head>

<body>
    <Routes @rendermode="RenderMode.InteractiveServer" />
    <FluentProviders />
    <script src="_framework/blazor.web.js"></script>
</body>

</html>
