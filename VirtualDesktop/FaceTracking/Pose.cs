namespace VirtualDesktop.FaceTracking
{
    public struct Pose
    {
        public static readonly Pose Identity = new Pose()
        {
            Orientation = Quaternion.Identity
        };
        public Quaternion Orientation;
        public Vector3 Position;
    }
}