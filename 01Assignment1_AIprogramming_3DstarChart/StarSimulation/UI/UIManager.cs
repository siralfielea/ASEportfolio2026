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

    public void Render(StarCatalog catalog, Camera camera, Vector2D<int> windowSize)
    {
        RenderSidebar(catalog);
        RenderInspectionPanel(windowSize);

        _imGuiController.Render();
    }

    private void RenderSidebar(StarCatalog catalog)
    {
        // Dock sidebar on the left
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(280, 480), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Star Catalog", ImGuiWindowFlags.None))
        {
            ImGui.Text($"Catalog Count: {catalog.Stars.Count}");
            ImGui.Separator();

            if (ImGui.BeginChild("StarListScroll"))
            {
                foreach (var star in catalog.Stars)
                {
                    bool isSelected = (SelectedStar == star);

                    if (ImGui.Selectable(star.DisplayName, isSelected))
                    {
                        SelectedStar = star;
                        OnFlyToRequested?.Invoke(star);
                    }
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
