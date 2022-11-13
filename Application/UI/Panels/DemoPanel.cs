using ImGuiNET;

namespace Application.UI.Panels;

public class DemoPanel : Panel
{
    public override void Attach()
    {
        Open = true;
    }

    public override void Detach()
    {
        Open = false;
    }

    public override void Update(float dt)
    {
    }

    public override void Render()
    {
        var isOpen = Open;
        if (!isOpen) return;
        
        ImGui.ShowDemoWindow(ref isOpen);

        if (!isOpen) Detach();
    }
}