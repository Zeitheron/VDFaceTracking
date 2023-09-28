namespace VirtualDesktop.FaceTracking
{
    public struct Quaternion
    {
        public static readonly Quaternion Identity = new Quaternion(0.0f, 0.0f, 0.0f, 1f);
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Quaternion(float x, float y, float z, float w)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }
    }
}