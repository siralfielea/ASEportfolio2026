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
    private string _searchFilter = "";
    private bool _properNamedOnly = true;

    // Sorting state
    private int _selectedSortIndex = 0;
    private int _lastSortIndex = -1;
    private bool _lastProperNamedOnly = true;

    private readonly string[] _sortOptions = new string[]
    {
        "Size (Largest First)",
        "Size (Smallest First)",
        "Brightness (Brightest First)",
        "Brightness (Dimmest First)",
        "Distance (Nearest First)",
        "Distance (Farthest First)",
        "Temperature (Hottest First)",
        "Temperature (Coolest First)",
        "Name (A - Z)"
    };

    public Star? SelectedStar { get; private set; }
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
        if (_selectedSortIndex != _lastSortIndex || _properNamedOnly != _lastProperNamedOnly)
        {
            ApplySort(catalog);
            _lastSortIndex = _selectedSortIndex;
            _lastProperNamedOnly = _properNamedOnly;
        }

        RenderWorldOverlays(catalog, camera, windowSize);
        RenderSidebar(catalog);
        RenderInspectionPanel(windowSize);

        _imGuiController.Render();
    }

    private void ApplySort(StarCatalog catalog)
    {
        catalog.Stars.Sort((a, b) =>
        {
            if (_properNamedOnly)
            {
                if (a.HasProperName && !b.HasProperName) return -1;
                if (!a.HasProperName && b.HasProperName) return 1;
            }

            return _selectedSortIndex switch
            {
                0 => b.SolarDiameter.CompareTo(a.SolarDiameter),
                1 => a.SolarDiameter.CompareTo(b.SolarDiameter),
                2 => a.Magnitude.CompareTo(b.Magnitude),
                3 => b.Magnitude.CompareTo(a.Magnitude),
                4 => a.Position.LengthSquared().CompareTo(b.Position.LengthSquared()),
                5 => b.Position.LengthSquared().CompareTo(a.Position.LengthSquared()),
                6 => b.Temperature.CompareTo(a.Temperature),
                7 => a.Temperature.CompareTo(b.Temperature),
                8 => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
                _ => 0
            };
        });
    }

    private void RenderWorldOverlays(StarCatalog catalog, Camera camera, Vector2D<int> windowSize)
    {
        var drawList = ImGui.GetBackgroundDrawList();
        Matrix4x4 viewProj = camera.GetViewMatrix() * camera.GetProjectionMatrix();

        foreach (var star in catalog.Stars)
        {
            Vector4 clipPos = Vector4.Transform(new Vector4(star.Position, 1.0f), viewProj);

            if (clipPos.W <= 0.1f) continue;

            Vector3 ndc = new Vector3(clipPos.X, clipPos.Y, clipPos.Z) / clipPos.W;

            if (ndc.X < -1.1f || ndc.X > 1.1f || ndc.Y < -1.1f || ndc.Y > 1.1f) continue;

            float screenX = (ndc.X + 1.0f) * 0.5f * windowSize.X;
            float screenY = (1.0f - ndc.Y) * 0.5f * windowSize.Y;

            float distToCamera = Vector3.Distance(camera.Position, star.Position);

            if (SelectedStar == star)
            {
                float boxHalfSize = 14.0f;
                Vector2 pMin = new Vector2(screenX - boxHalfSize, screenY - boxHalfSize);
                Vector2 pMax = new Vector2(screenX + boxHalfSize, screenY + boxHalfSize);

                uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.9f, 1.0f, 0.95f));
                uint glowCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.9f, 1.0f, 0.25f));

                drawList.AddRect(pMin, pMax, borderCol, 0.0f, ImDrawFlags.None, 1.5f);
                drawList.AddRect(pMin - new Vector2(2f), pMax + new Vector2(2f), glowCol, 0.0f, ImDrawFlags.None, 1.0f);

                string selectText = $"{star.DisplayName} ({distToCamera:F1} ly)";
                drawList.AddText(new Vector2(screenX + boxHalfSize + 5, screenY - 7), borderCol, selectText);
            }
            else if (star.HasProperName)
            {
                float maxVisibilityRange = 1200.0f;
                float fadeStart = 150.0f;
                float alpha = 1.0f - Math.Clamp((distToCamera - fadeStart) / (maxVisibilityRange - fadeStart), 0.0f, 1.0f);

                if (distToCamera < maxVisibilityRange && alpha > 0.05f)
                {
                    uint textCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.92f, 1.0f, alpha * 0.85f));
                    drawList.AddText(new Vector2(screenX + 8, screenY - 6), textCol, star.ProperName!);
                }
            }
        }
    }

    private void RenderSidebar(StarCatalog catalog)
    {
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(320, 520), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Star Catalog", ImGuiWindowFlags.None))
        {
            ImGui.TextColored(new Vector4(0.7f, 0.9f, 1.0f, 1.0f), $"Total Loaded: {catalog.Stars.Count:N0} stars");
            ImGui.Spacing();

            ImGui.InputText("Search", ref _searchFilter, 64);
            ImGui.Checkbox("Proper Named Only", ref _properNamedOnly);

            ImGui.SetNextItemWidth(-1);
            ImGui.Combo("##SortCombo", ref _selectedSortIndex, _sortOptions, _sortOptions.Length);

            ImGui.Separator();

            if (ImGui.BeginChild("StarListScroll"))
            {
                int renderedCount = 0;
                int maxDisplay = 100;

                foreach (var star in catalog.Stars)
                {
                    if (_properNamedOnly && !star.HasProperName) continue;
                    if (!_properNamedOnly && !star.HasProperName && !star.HasDesignation) continue;

                    if (!string.IsNullOrWhiteSpace(_searchFilter) &&
                        !star.DisplayName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool isSelected = (SelectedStar == star);

                    // Uses GetFormattedDiameterKm() or GetFormattedSolarDiameter() depending on sort
                    string itemLabel = _selectedSortIndex switch
                    {
                        0 or 1 => $"{star.DisplayName} ({star.GetFormattedDiameterKm()})",
                        2 or 3 => $"{star.DisplayName} (Mag {star.Magnitude:F1})",
                        4 or 5 => $"{star.DisplayName} ({star.Position.Length():F1} ly)",
                        6 or 7 => $"{star.DisplayName} ({star.Temperature:N0} K)",
                        _ => star.DisplayName
                    };

                    if (ImGui.Selectable(itemLabel, isSelected))
                    {
                        SelectedStar = star;
                        OnFlyToRequested?.Invoke(star);
                    }

                    renderedCount++;
                    if (renderedCount >= maxDisplay) break;
                }

                ImGui.EndChild();
            }
            ImGui.End();
        }
    }

    private void RenderInspectionPanel(Vector2D<int> windowSize)
    {
        if (SelectedStar == null) return;

        float panelWidth = 500;
        float panelHeight = 180;
        float posX = (windowSize.X - panelWidth) / 2.0f;
        float posY = windowSize.Y - panelHeight - 15;

        ImGui.SetNextWindowPos(new Vector2(posX, posY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("Star Inspector", flags))
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), SelectedStar.DisplayName);
            if (SelectedStar.HasDesignation && SelectedStar.HasProperName)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({SelectedStar.Designation})");
            }
            ImGui.Separator();

            ImGui.Columns(2, "InspectorColumns", false);

            float distFromSol = SelectedStar.Position.Length();
            ImGui.Text("Coordinates (3D):");
            ImGui.Text($"X: {SelectedStar.Position.X:F2} ly");
            ImGui.Text($"Y: {SelectedStar.Position.Y:F2} ly");
            ImGui.Text($"Z: {SelectedStar.Position.Z:F2} ly");
            ImGui.Text($"Dist to Sol: {distFromSol:N1} ly");

            ImGui.NextColumn();

            ImGui.Text("Physical Properties:");
            ImGui.Text($"Temp:     {SelectedStar.Temperature:N0} K");
            ImGui.Text($"Diameter: {SelectedStar.GetFormattedSolarDiameter()} ({SelectedStar.GetFormattedDiameterKm()})");
            ImGui.Text($"Mag:      {SelectedStar.Magnitude:F2}");
            ImGui.Text($"Lum:      {SelectedStar.Luminosity:N1} Sol");

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
