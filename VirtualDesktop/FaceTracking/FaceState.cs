using System.Runtime.InteropServices;

namespace VirtualDesktop.FaceTracking
{
    public struct FaceState
    {
        public const int ExpressionCount = 63;
        public const int ConfidenceCount = 2;
        public static readonly FaceState Identity = new FaceState()
        {
            LeftEyePose = Pose.Identity,
            RightEyePose = Pose.Identity
        };

        [MarshalAs(UnmanagedType.I1)]
        public bool FaceIsValid;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsEyeFollowingBlendshapesValid;

        public unsafe fixed float ExpressionWeights[63];

        public unsafe fixed float ExpressionConfidences[2];

        [MarshalAs(UnmanagedType.I1)]
        public bool LeftEyeIsValid;

        [MarshalAs(UnmanagedType.I1)]
        public bool RightEyeIsValid;

        public Pose LeftEyePose;
        public Pose RightEyePose;

        public float LeftEyeConfidence;
        public float RightEyeConfidence;
    }
}