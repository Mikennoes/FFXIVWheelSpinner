using System;
using System.Numerics;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;

namespace FCWheel.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string newName = string.Empty;
    private string bulkNames = string.Empty;
    private string winnerName = string.Empty;
    private bool isBulkMode = false;

    private string feedbackMessage = string.Empty;
    private DateTime feedbackTime;

    private bool isSpinning = false;
    private float currentAngle = 0f;
    private float spinSpeed = 0f;
    private const float Drag = 0.982f;
    private readonly Random random = new();

    public MainWindow(Plugin plugin)
        : base("FCWheel Prize Spinner##FCWheelMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 800),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();

        DrawInputSection();
        ImGuiHelpers.ScaledDummy(5.0f);
        DrawChatSettings();
        ImGuiHelpers.ScaledDummy(5.0f);
        DrawListSection();
        ImGui.Separator();
        DrawWheelSection();

        UpdatePhysics();
    }

    private void DrawHeader()
    {
        // Title
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "FC Prize Spinner");

        // Settings Button in top right
        float buttonSize = 25f * ImGuiHelpers.GlobalScale;
        ImGui.SameLine(ImGui.GetWindowWidth() - buttonSize - ImGui.GetStyle().WindowPadding.X - (15 * ImGuiHelpers.GlobalScale));

        ImGui.PushFont(UiBuilder.IconFont);
        try
        {
            if (ImGui.Button($"{(char)FontAwesomeIcon.Cog}##ConfigBtn", new Vector2(buttonSize, buttonSize)))
            {
                plugin.ToggleConfigUi();
            }
        }
        finally
        {
            ImGui.PopFont();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open Flavor Text Settings");
        }
    }

    private void DrawInputSection()
    {
        ImGui.Checkbox("Bulk Entry Mode", ref isBulkMode);

        if (isBulkMode)
        {
            ImGui.InputTextMultiline("##BulkInput", ref bulkNames, 10000, new Vector2(-1, 80 * ImGuiHelpers.GlobalScale));
            if (ImGui.Button("Add All From List", new Vector2(-1, 30 * ImGuiHelpers.GlobalScale))) ProcessBulkNames();
        }
        else
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            float buttonWidth = (availWidth - ImGui.GetStyle().ItemSpacing.X) / 2;

            if (ImGui.Button("Add Target", new Vector2(buttonWidth, 30 * ImGuiHelpers.GlobalScale))) AddTarget();
            ImGui.SameLine();
            if (ImGui.Button("Add Nearby", new Vector2(buttonWidth, 30 * ImGuiHelpers.GlobalScale))) AddNearbyPlayers();

            if (!string.IsNullOrEmpty(feedbackMessage) && (DateTime.Now - feedbackTime).TotalSeconds < 3)
            {
                var color = feedbackMessage.Contains("Added") ? new Vector4(0.4f, 1f, 0.4f, 1f) : new Vector4(1f, 0.4f, 0.4f, 1f);
                ImGui.TextColored(color, feedbackMessage);
            }
            else { ImGuiHelpers.ScaledDummy(17.0f); }

            ImGui.AlignTextToFramePadding();
            ImGui.Text("Manual Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-70 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText("##NameInput", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue)) AddName();
            ImGui.SameLine();
            if (ImGui.Button("Add", new Vector2(60 * ImGuiHelpers.GlobalScale, 0))) AddName();
        }
    }

    private void AddTarget()
    {
        var target = Plugin.TargetManager.Target;
        if (target == null) { SetFeedback("No target selected!"); return; }
        if (target.ObjectKind != ObjectKind.Player) { SetFeedback("Target is not a player!"); return; }

        string name = target.Name.TextValue;
        if (plugin.Configuration.Names.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            SetFeedback($"{name} is a duplicate!");
            return;
        }

        plugin.Configuration.Names.Add(name);
        plugin.Configuration.Save();
        SetFeedback($"Added {name}!");
    }

    private void AddNearbyPlayers()
    {
        float range = 25.0f;
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null) return;

        int addedCount = 0;
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind == ObjectKind.Player &&
                obj.EntityId != localPlayer.EntityId &&
                Vector3.Distance(localPlayer.Position, obj.Position) <= range)
            {
                string name = obj.Name.TextValue;
                if (!plugin.Configuration.Names.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    plugin.Configuration.Names.Add(name);
                    addedCount++;
                }
            }
        }

        if (addedCount > 0) { plugin.Configuration.Save(); SetFeedback($"Added {addedCount} players!"); }
        else { SetFeedback("No new players found."); }
    }

    private void DrawChatSettings()
    {
        if (ImGui.CollapsingHeader("Chat Announcement Settings"))
        {
            var announce = plugin.Configuration.AnnounceWinner;
            if (ImGui.Checkbox("Announce to Chat", ref announce)) { plugin.Configuration.AnnounceWinner = announce; plugin.Configuration.Save(); }

            if (announce)
            {
                ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
                var channelIdx = plugin.Configuration.SelectedChannelIndex;
                if (ImGui.Combo("Output Channel", ref channelIdx, plugin.Configuration.Channels, plugin.Configuration.Channels.Length))
                {
                    plugin.Configuration.SelectedChannelIndex = channelIdx;
                    plugin.Configuration.Save();
                }
            }
        }
    }

    private void DrawListSection()
    {
        ImGui.Text($"Entrants ({plugin.Configuration.Names.Count})");
        using (var child = ImRaii.Child("NameList", new Vector2(-1, 120 * ImGuiHelpers.GlobalScale), true))
        {
            if (child.Success)
            {
                for (int i = 0; i < plugin.Configuration.Names.Count; i++)
                {
                    if (ImGui.Selectable($"{plugin.Configuration.Names[i]}##{i}")) { }
                    if (ImGui.BeginPopupContextItem($"##Pop{i}", ImGuiPopupFlags.MouseButtonRight))
                    {
                        if (ImGui.MenuItem("Remove")) { plugin.Configuration.Names.RemoveAt(i); plugin.Configuration.Save(); }
                        ImGui.EndPopup();
                    }
                }
            }
        }
        if (ImGui.Button("Clear All List", new Vector2(-1, 25 * ImGuiHelpers.GlobalScale))) { plugin.Configuration.Names.Clear(); plugin.Configuration.Save(); winnerName = string.Empty; }
    }

    private void DrawWheelSection()
    {
        Vector2 content = ImGui.GetContentRegionAvail();
        float radius = 120f * ImGuiHelpers.GlobalScale;
        Vector2 center = ImGui.GetCursorScreenPos() + new Vector2(content.X / 2, radius + 20);
        var drawList = ImGui.GetWindowDrawList();
        int count = plugin.Configuration.Names.Count;

        if (count > 0)
        {
            float segment = (float)(Math.PI * 2 / count);
            for (int i = 0; i < count; i++)
            {
                float start = currentAngle + (i * segment);
                float end = start + segment;
                uint color = (i % 2 == 0) ? ImGui.GetColorU32(new Vector4(0.74f, 0.64f, 0.42f, 1f)) : ImGui.GetColorU32(new Vector4(0.12f, 0.23f, 0.35f, 1f));

                drawList.PathLineTo(center);
                drawList.PathArcTo(center, radius, start, end, 32);
                drawList.PathFillConvex(color);

                string displayName = plugin.Configuration.Names[i];
                if (displayName.Length > 10) displayName = displayName[..8] + "..";

                float textAng = start + (segment / 2f);
                Vector2 textDir = new Vector2((float)Math.Cos(textAng), (float)Math.Sin(textAng));
                Vector2 textPos = center + textDir * (radius * 0.60f);

                Vector2 textSize = ImGui.CalcTextSize(displayName) * 0.85f;
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 0.85f, textPos - (textSize / 2), 0xFFFFFFFF, displayName);
            }
            drawList.AddTriangleFilled(center + new Vector2(-15, -radius - 15), center + new Vector2(15, -radius - 15), center + new Vector2(0, -radius + 5), 0xFF00FFFF);
        }

        ImGuiHelpers.ScaledDummy((radius * 2) + 45);

        if (!string.IsNullOrEmpty(winnerName) && !isSpinning)
        {
            ImGui.SetWindowFontScale(1.3f);
            ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), $"WINNER: {winnerName}!");
            ImGui.SetWindowFontScale(1.0f);
        }

        using (var dis = ImRaii.Disabled(isSpinning || count == 0))
        {
            if (ImGui.Button("SPIN THE WHEEL", new Vector2(-1, 45 * ImGuiHelpers.GlobalScale)))
            {
                winnerName = string.Empty;
                isSpinning = true;
                spinSpeed = (float)(random.NextDouble() * 18.0 + 12.0);
                AnnounceToChat(plugin.Configuration.SpinMessage);
            }
        }
    }

    private void UpdatePhysics()
    {
        if (!isSpinning) return;
        currentAngle += spinSpeed * ImGui.GetIO().DeltaTime;
        spinSpeed *= Drag;
        if (spinSpeed < 0.12f)
        {
            isSpinning = false;
            spinSpeed = 0;
            int count = plugin.Configuration.Names.Count;
            if (count > 0)
            {
                float pointer = (float)(-Math.PI / 2);
                float rel = (pointer - currentAngle) % (float)(Math.PI * 2);
                while (rel < 0) rel += (float)(Math.PI * 2);
                int index = (int)(rel / (Math.PI * 2 / count)) % count;
                winnerName = plugin.Configuration.Names[index];

                string message = plugin.Configuration.WinMessage.Replace("{name}", winnerName);
                AnnounceToChat(message);
            }
        }
    }

    private void AnnounceToChat(string message)
    {
        if (!plugin.Configuration.AnnounceWinner) return;
        string chan = plugin.Configuration.Channels[plugin.Configuration.SelectedChannelIndex];
        string fullMessage = $"{chan} {message}";

        unsafe
        {
            var str = Utf8String.FromString(fullMessage);
            str->SanitizeString(AllowedEntities.Numbers | AllowedEntities.UppercaseLetters | AllowedEntities.LowercaseLetters | AllowedEntities.SpecialCharacters | AllowedEntities.OtherCharacters);
            if (str->StringLength <= 500) UIModule.Instance()->ProcessChatBoxEntry(str);
            str->Dtor();
        }
    }

    private void AddName()
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        string trimmed = newName.Trim();
        if (plugin.Configuration.Names.Any(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase))) { SetFeedback($"{trimmed} is already entered!"); return; }
        plugin.Configuration.Names.Add(trimmed);
        plugin.Configuration.Save();
        SetFeedback($"Added {trimmed}!");
        newName = string.Empty;
    }

    private void SetFeedback(string msg) { feedbackMessage = msg; feedbackTime = DateTime.Now; }

    private void ProcessBulkNames()
    {
        if (string.IsNullOrWhiteSpace(bulkNames)) return;
        var split = bulkNames.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        int addedCount = 0;
        foreach (var name in split)
        {
            string trimmed = name.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !plugin.Configuration.Names.Any(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                plugin.Configuration.Names.Add(trimmed);
                addedCount++;
            }
        }
        plugin.Configuration.Save();
        SetFeedback($"Bulk added {addedCount} names!");
        bulkNames = string.Empty;
        isBulkMode = false;
    }
}