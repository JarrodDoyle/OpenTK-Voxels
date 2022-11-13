namespace Application.UI;

public abstract class Panel
{
    public bool Open { get; set; }

    public abstract void Attach();
    public abstract void Detach();
    public abstract void Update(float dt);
    public abstract void Render();
}