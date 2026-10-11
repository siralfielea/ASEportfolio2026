using System;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using StarChart.Navigation;

namespace StarChart.UI;

public class UIManager : IDisposable
{
    private readonly ImGuiController _imGuiController;
    public Star? SelectedStar { get; private set; }

    // Event fired when user selects a star to fly to
    public event Action<Star>? OnFlyToRequested;

    public UIManager(GL gl, IWindow window, IInputContext input)
    {
        _imGuiController = new ImGuiController(gl, window, input);
    }

    public void Update(float deltaTime)
    {
        _imGuiController.Update(deltaTime);
    }

    public void SelectStar(Star star)
    {
        SelectedStar = star;
    }

    public void Render(StarCatalog catalog, Camera camera, Vector2D<int> windowSize)
    {
        // 1. Draw 2D floating text overlays and selection boxes in 3D world-to-screen space
        RenderWorldOverlays(catalog, camera, windowSize);

        // 2. Render standard ImGui Sidebar and Inspection window boxes
        RenderSidebar(catalog);
        RenderInspectionPanel(windowSize);

        _imGuiController.Render();
    }

    private void RenderWorldOverlays(StarCatalog catalog, Camera camera, Vector2D<int> windowSize)
    {
        // Get the background draw list to paint overlay markup underneath UI windows
        var drawList = ImGui.GetBackgroundDrawList();
        
        // Calculate the view-projection transformation matrix
        Matrix4x4 viewProj = camera.GetViewMatrix() * camera.GetProjectionMatrix();

        foreach (var star in catalog.Stars)
        {
            // Project 3D star position to Clip Space
            Vector4 clipPos = Vector4.Transform(new Vector4(star.Position, 1.0f), viewProj);

            // Ignore stars behind camera near clipping plane (Z perspective)
            if (clipPos.W <= 0.1f) continue;

            // Convert to Normalized Device Coordinates (NDC)
            Vector3 ndc = new Vector3(clipPos.X, clipPos.Y, clipPos.Z) / clipPos.W;

            // Ignore outer view frustum margins (off-screen clipping)
            if (ndc.X < -1.1f || ndc.X > 1.1f || ndc.Y < -1.1f || ndc.Y > 1.1f) continue;

            // Map clip space coordinates to screen pixels
            float screenX = (ndc.X + 1.0f) * 0.5f * windowSize.X;
            float screenY = (1.0f - ndc.Y) * 0.5f * windowSize.Y;

            // Calculate current camera-to-star distance for perspective adjustments
            float distToCamera = Vector3.Distance(camera.Position, star.Position);

            // 1. Render Selection HUD around target star
            if (SelectedStar == star)
            {
                float boxHalfSize = 14.0f; // Radius of selection box in pixels
                Vector2 pMin = new Vector2(screenX - boxHalfSize, screenY - boxHalfSize);
                Vector2 pMax = new Vector2(screenX + boxHalfSize, screenY + boxHalfSize);

                // Cyan Target Bracket highlight colors
                uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.9f, 1.0f, 0.95f)); // Bright Cyan ABGR
                uint glowCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.9f, 1.0f, 0.25f));   // Soft glow backdrop

                // Draw pulsing HUD box outlines around target
                drawList.AddRect(pMin, pMax, borderCol, 0.0f, ImDrawFlags.None, 1.5f);
                drawList.AddRect(pMin - new Vector2(2f), pMax + new Vector2(2f), glowCol, 0.0f, ImDrawFlags.None, 1.0f);

                // Floating data inspect next to target cursor
                string selectText = $"{star.DisplayName} ({distToCamera:F1} ly)";
                drawList.AddText(new Vector2(screenX + boxHalfSize + 5, screenY - 7), borderCol, selectText);
            }
            // 2. Render Name Labels for normal Stars
            else if (star.IsNamed)
            {
                // Smoothly fade out small text labels based on camera depth distance to keep UI legible
                float maxVisibilityRange = 1200.0f;
                float fadeStart = 150.0f;
                float alpha = 1.0f - Math.Clamp((distToCamera - fadeStart) / (maxVisibilityRange - fadeStart), 0.0f, 1.0f);

                if (distToCamera < maxVisibilityRange && alpha > 0.05f)
                {
                    uint textCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.92f, 1.0f, alpha * 0.85f)); // Soft blue-white
                    drawList.AddText(new Vector2(screenX + 8, screenY - 6), textCol, star.Name!);
                }
            }
        }
    }

private string _searchFilter = "";

private void RenderSidebar(StarCatalog catalog)
{
    ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
    ImGui.SetNextWindowSize(new Vector2(280, 480), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Star Catalog", ImGuiWindowFlags.None))
    {
        ImGui.Text($"Catalog Count: {catalog.Stars.Count}");
        
        // Search Filter Input
        ImGui.InputText("Search", ref _searchFilter, 64);
        ImGui.Separator();

        if (ImGui.BeginChild("StarListScroll"))
        {
            int renderedCount = 0;
            int maxDisplay = 100; // Cap UI list items per frame

            foreach (var star in catalog.Stars)
            {
                // Filter by name or ID if a search term is typed
                if (!string.IsNullOrWhiteSpace(_searchFilter) &&
                    !star.DisplayName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isSelected = (SelectedStar == star);
                if (ImGui.Selectable(star.DisplayName, isSelected))
                {
                    SelectedStar = star;
                    OnFlyToRequested?.Invoke(star);
                }

                renderedCount++;
                if (renderedCount >= maxDisplay) break; // Keep frame times ultra-fast
            }

            ImGui.EndChild();
        }
        ImGui.End();
    }
}
    private void RenderInspectionPanel(Vector2D<int> windowSize)
    {
        if (SelectedStar == null) return;

        float panelWidth = 450;
        float panelHeight = 160;
        float posX = (windowSize.X - panelWidth) / 2.0f;
        float posY = windowSize.Y - panelHeight - 15;

        ImGui.SetNextWindowPos(new Vector2(posX, posY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("Star Inspector", flags))
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), SelectedStar.DisplayName);
            ImGui.Separator();

            ImGui.Columns(2, "InspectorColumns", false);

            ImGui.Text("Coordinates:");
            ImGui.Text($"X: {SelectedStar.Position.X:F2} ly");
            ImGui.Text($"Y: {SelectedStar.Position.Y:F2} ly");
            ImGui.Text($"Z: {SelectedStar.Position.Z:F2} ly");

            ImGui.NextColumn();

            ImGui.Text($"Temp: {SelectedStar.Temperature:F0} K");
            ImGui.Text($"Mag:  {SelectedStar.Magnitude:F2}");
            ImGui.Text($"Lum:  {SelectedStar.Luminosity:F2} Sol");

            ImGui.Columns(1);
            ImGui.Spacing();

            if (ImGui.Button("Fly To Star", new Vector2(-1, 28)))
            {
                OnFlyToRequested?.Invoke(SelectedStar);
            }

            ImGui.End();
        }
    }

    public void Dispose()
    {
        _imGuiController.Dispose();
    }
}
