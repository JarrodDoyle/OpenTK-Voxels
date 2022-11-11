using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Application;

public class Camera
{
    private Vector3 _front = -Vector3.UnitZ;
    private Vector3 _up = Vector3.UnitY;
    private Vector3 _right = Vector3.UnitX;

    private float _pitch;
    private float _yaw = -MathHelper.PiOver2;
    private float _fov = MathHelper.PiOver2;
    private bool _updated;

    public float MoveSpeed { get; set; } = 1f;
    public float MouseSensitivity { get; set; } = 1f;

    public Camera(Vector3 position, float aspectRatio)
    {
        Position = position;
        AspectRatio = aspectRatio;
        _updated = true;
    }

    public Vector3 Position { get; set; }
    public float AspectRatio { private get; set; }

    public float Pitch
    {
        get => MathHelper.RadiansToDegrees(_pitch);
        set
        {
            _pitch = MathHelper.DegreesToRadians(MathHelper.Clamp(value, -89f, 89f));
            UpdateVectors();
        }
    }

    public float Yaw
    {
        get => MathHelper.RadiansToDegrees(_yaw);
        set
        {
            _yaw = MathHelper.DegreesToRadians(value);
            UpdateVectors();
        }
    }

    public float Fov
    {
        get => MathHelper.RadiansToDegrees(_fov);
        set => _fov = MathHelper.DegreesToRadians(MathHelper.Clamp(value, 1f, 90f));
    }

    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(Position, Position + _front, _up);
    }

    public Matrix4 GetProjectionMatrix()
    {
        return Matrix4.CreatePerspectiveFieldOfView(_fov, AspectRatio, 0.01f, 100f);
    }

    private void UpdateVectors()
    {
        // First, the front matrix is calculated using some basic trigonometry.
        _front.X = MathF.Cos(_pitch) * MathF.Cos(_yaw);
        _front.Y = MathF.Sin(_pitch);
        _front.Z = MathF.Cos(_pitch) * MathF.Sin(_yaw);

        // We need to make sure the vectors are all normalized, as otherwise we would get some funky results.
        _front = Vector3.Normalize(_front);

        // Calculate both the right and the up vector using cross product.
        _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY));
        _up = Vector3.Normalize(Vector3.Cross(_right, _front));
    }

    public void ProcessInputs(float dt, KeyboardState keyState, MouseState mouseState)
    {
        // Mouse updates
        var mouseDeltaX = mouseState.X - mouseState.PreviousX;
        var mouseDeltaY = mouseState.Y - mouseState.PreviousY;
        Yaw += mouseDeltaX * MouseSensitivity;
        Pitch -= mouseDeltaY * MouseSensitivity;

        // Keyboard updates
        var oldPos = Position;
        if (keyState.IsKeyDown(Keys.W)) Position += _front * MoveSpeed * dt;
        if (keyState.IsKeyDown(Keys.S)) Position -= _front * MoveSpeed * dt;
        if (keyState.IsKeyDown(Keys.D)) Position += _right * MoveSpeed * dt;
        if (keyState.IsKeyDown(Keys.A)) Position -= _right * MoveSpeed * dt;
        if (keyState.IsKeyDown(Keys.Q)) Position += _up * MoveSpeed * dt;
        if (keyState.IsKeyDown(Keys.E)) Position -= _up * MoveSpeed * dt;

        // Set the updated flag
        if (Position != oldPos || mouseDeltaX != 0 || mouseDeltaY != 0) _updated = true;
    }

    public bool ProcessUpdate()
    {
        var wasUpdated = _updated;
        _updated = false;
        return wasUpdated;
    }
}