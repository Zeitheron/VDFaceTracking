using Elements.Core;
using FrooxEngine;
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using VirtualDesktop.FaceTracking;

namespace VDFaceTracking
{
    public class VDProxy : IInputDriver
    {
        private const string BodyStateMapName = "VirtualDesktop.BodyState";
        private const string BodyStateEventName = "VirtualDesktop.BodyStateEvent";
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _mappedView;
        private unsafe FaceState* _faceState;
        private EventWaitHandle _faceStateEvent;

        private CancellationTokenSource cancellationTokenSource;

        private bool? _isTracking;

        private Thread thread;

        private const int NATURAL_EXPRESSIONS_COUNT = FBExpression.Max;
        private const float SRANIPAL_NORMALIZER = 0.75f;
        private float[] expressions = new float[NATURAL_EXPRESSIONS_COUNT + (8 * 2)];

        private double pitch_L, yaw_L, pitch_R, yaw_R; // Eye rotations

        #region RESONITE VARIABLES
        private InputInterface _input;
        public int UpdateOrder => 100;
        private Mouth mouth;
        private Eyes eyes;
        #endregion

        private bool? IsTracking
        {
            get => this._isTracking;
            set
            {
                bool? nullable = value;
                bool? isTracking = this._isTracking;
                if (nullable.GetValueOrDefault() == isTracking.GetValueOrDefault() & nullable.HasValue == isTracking.HasValue)
                    return;
                this._isTracking = value;
                if (value.Value)
                    VDFaceTracking.Msg("Tracking is now active!");
                else
                    VDFaceTracking.Msg("Tracking is not active. Make sure you are connected to your computer, a VR game or SteamVR is launched and 'Forward tracking data' is enabled in the Streaming tab.");
            }
        }

        public virtual void UpdateThread()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    Update();
                }
                catch
                {
                }
            }
        }

        public virtual unsafe void Update()
        {
            if (this._faceStateEvent.WaitOne(50))
            {
                this.UpdateTracking();
            }
            else
            {
                FaceState* faceState = this._faceState;
                this.IsTracking = new bool?((IntPtr)faceState != IntPtr.Zero && (faceState->LeftEyeIsValid || faceState->RightEyeIsValid || faceState->IsEyeFollowingBlendshapesValid || faceState->FaceIsValid));
            }
        }

        private unsafe void UpdateTracking()
        {
            bool flag = false;
            FaceState* faceState = this._faceState;
            if ((IntPtr)faceState != IntPtr.Zero)
            {
                float* expressionWeights = faceState->ExpressionWeights;

                if (faceState->LeftEyeIsValid)
                {
                    Pose leftEyePose = faceState->LeftEyePose;

                    expressions[FBExpression.LeftRot_x] = leftEyePose.Orientation.X;
                    expressions[FBExpression.LeftRot_y] = leftEyePose.Orientation.Y;
                    expressions[FBExpression.LeftRot_z] = leftEyePose.Orientation.Z;
                    expressions[FBExpression.LeftRot_w] = leftEyePose.Orientation.W;

                    expressions[FBExpression.LeftPos_x] = leftEyePose.Position.X;
                    expressions[FBExpression.LeftPos_y] = leftEyePose.Position.Y;
                    expressions[FBExpression.LeftPos_z] = leftEyePose.Position.Z;

                    flag = true;
                }

                if(faceState->RightEyeIsValid)
                {
                    Pose rightEyePose = faceState->RightEyePose;

                    expressions[FBExpression.RightRot_x] = rightEyePose.Orientation.X;
                    expressions[FBExpression.RightRot_y] = rightEyePose.Orientation.Y;
                    expressions[FBExpression.RightRot_z] = rightEyePose.Orientation.Z;
                    expressions[FBExpression.RightRot_w] = rightEyePose.Orientation.W;

                    expressions[FBExpression.RightPos_x] = rightEyePose.Position.X;
                    expressions[FBExpression.RightPos_y] = rightEyePose.Position.Y;
                    expressions[FBExpression.RightPos_z] = rightEyePose.Position.Z;

                    flag = true;
                }

                if (faceState->FaceIsValid && faceState->IsEyeFollowingBlendshapesValid)
                {
                    for(int i = 0; i < NATURAL_EXPRESSIONS_COUNT; ++i)
                        expressions[i] = expressionWeights[i];

                    flag = true;
                }
            }
            this.IsTracking = new bool?(flag);
        }

        internal unsafe bool Initialize()
        {
            try
            {
                int size = Marshal.SizeOf<FaceState>();
                this._mappedFile = MemoryMappedFile.OpenExisting(BodyStateMapName, MemoryMappedFileRights.ReadWrite);
                this._mappedView = this._mappedFile.CreateViewAccessor(0L, (long)size);

                byte* numPtr = null;
                _mappedView.SafeMemoryMappedViewHandle.AcquirePointer(ref numPtr);
                this._faceState = (FaceState*) numPtr;
                this._faceStateEvent = EventWaitHandle.OpenExisting(BodyStateEventName);
                VDFaceTracking.Msg("Opened MemoryMappedFile. Everything should be working!");

                cancellationTokenSource = new CancellationTokenSource();
                thread = new Thread(UpdateThread);
                thread.Start();

                return true;
            }
            catch
            {
                VDFaceTracking.Error("Failed to open MemoryMappedFile. Make sure the Virtual Desktop Streamer (v1.30 or later) is running.");
                return false;
            }
        }

        private void PrepareUpdate()
        {
            // Eye Expressions

            double q_x = expressions[FBExpression.LeftRot_x];
            double q_y = expressions[FBExpression.LeftRot_y];
            double q_z = expressions[FBExpression.LeftRot_z];
            double q_w = expressions[FBExpression.LeftRot_w];

            double yaw = Math.Atan2(2.0 * (q_y * q_z + q_w * q_x), q_w * q_w - q_x * q_x - q_y * q_y + q_z * q_z);
            double pitch = Math.Asin(-2.0 * (q_x * q_z - q_w * q_y));
            // Not needed for eye tracking
            // double roll = Math.Atan2(2.0 * (q_x * q_y + q_w * q_z), q_w * q_w + q_x * q_x - q_y * q_y - q_z * q_z); 

            // From radians
            pitch_L = 180.0 / Math.PI * pitch;
            yaw_L = 180.0 / Math.PI * yaw;

            q_x = expressions[FBExpression.RightRot_x];
            q_y = expressions[FBExpression.RightRot_y];
            q_z = expressions[FBExpression.RightRot_z];
            q_w = expressions[FBExpression.RightRot_w];

            yaw = Math.Atan2(2.0 * (q_y * q_z + q_w * q_x), q_w * q_w - q_x * q_x - q_y * q_y + q_z * q_z);
            pitch = Math.Asin(-2.0 * (q_x * q_z - q_w * q_y));

            // From radians
            pitch_R = 180.0 / Math.PI * pitch;
            yaw_R = 180.0 / Math.PI * yaw;

            // Face Expressions

            // Eyelid edge case, eyes are actually closed now
            if (expressions[FBExpression.Eyes_Look_Down_L] == expressions[FBExpression.Eyes_Look_Up_L] && expressions[FBExpression.Eyes_Closed_L] > 0.25f)
            {
                expressions[FBExpression.Eyes_Closed_L] = 0; // 0.9f - (expressions[FBExpression.Lid_Tightener_L] * 3);
            }
            else
            {
                expressions[FBExpression.Eyes_Closed_L] = 0.9f - ((expressions[FBExpression.Eyes_Closed_L] * 3) / (1 + expressions[FBExpression.Eyes_Look_Down_L] * 3));
            }

            // Another eyelid edge case
            if (expressions[FBExpression.Eyes_Look_Down_R] == expressions[FBExpression.Eyes_Look_Up_R] && expressions[FBExpression.Eyes_Closed_R] > 0.25f)
            {
                expressions[FBExpression.Eyes_Closed_R] = 0; // 0.9f - (expressions[FBExpression.Lid_Tightener_R] * 3);
            }
            else
            {
                expressions[FBExpression.Eyes_Closed_R] = 0.9f - ((expressions[FBExpression.Eyes_Closed_R] * 3) / (1 + expressions[FBExpression.Eyes_Look_Down_R] * 3));
            }

            //expressions[FBExpression.Lid_Tightener_L = 0.8f-expressions[FBExpression.Eyes_Closed_L]; // Sad: fix combined param instead
            //expressions[FBExpression.Lid_Tightener_R = 0.8f-expressions[FBExpression.Eyes_Closed_R]; // Sad: fix combined param instead

            if (1 - expressions[FBExpression.Eyes_Closed_L] < expressions[FBExpression.Lid_Tightener_L])
                expressions[FBExpression.Lid_Tightener_L] = (1 - expressions[FBExpression.Eyes_Closed_L]) - 0.01f;

            if (1 - expressions[FBExpression.Eyes_Closed_R] < expressions[FBExpression.Lid_Tightener_R])
                expressions[FBExpression.Lid_Tightener_R] = (1 - expressions[FBExpression.Eyes_Closed_R]) - 0.01f;

            //expressions[FBExpression.Lid_Tightener_L = Math.Max(0, expressions[FBExpression.Lid_Tightener_L] - 0.15f);
            //expressions[FBExpression.Lid_Tightener_R = Math.Max(0, expressions[FBExpression.Lid_Tightener_R] - 0.15f);

            expressions[FBExpression.Upper_Lid_Raiser_L] = Math.Max(0, expressions[FBExpression.Upper_Lid_Raiser_L] - 0.5f);
            expressions[FBExpression.Upper_Lid_Raiser_R] = Math.Max(0, expressions[FBExpression.Upper_Lid_Raiser_R] - 0.5f);

            expressions[FBExpression.Lid_Tightener_L] = Math.Max(0, expressions[FBExpression.Lid_Tightener_L] - 0.5f);
            expressions[FBExpression.Lid_Tightener_R] = Math.Max(0, expressions[FBExpression.Lid_Tightener_R] - 0.5f);

            expressions[FBExpression.Inner_Brow_Raiser_L] = Math.Min(1, expressions[FBExpression.Inner_Brow_Raiser_L] * 3f); // * 4;
            expressions[FBExpression.Brow_Lowerer_L] = Math.Min(1, expressions[FBExpression.Brow_Lowerer_L] * 3f); // * 4;
            expressions[FBExpression.Outer_Brow_Raiser_L] = Math.Min(1, expressions[FBExpression.Outer_Brow_Raiser_L] * 3f); // * 4;

            expressions[FBExpression.Inner_Brow_Raiser_R] = Math.Min(1, expressions[FBExpression.Inner_Brow_Raiser_R] * 3f); // * 4;
            expressions[FBExpression.Brow_Lowerer_R] = Math.Min(1, expressions[FBExpression.Brow_Lowerer_R] * 3f); // * 4;
            expressions[FBExpression.Outer_Brow_Raiser_R] = Math.Min(1, expressions[FBExpression.Outer_Brow_Raiser_R] * 3f); // * 4;

            expressions[FBExpression.Eyes_Look_Up_L] = expressions[FBExpression.Eyes_Look_Up_L] * 0.55f;
            expressions[FBExpression.Eyes_Look_Up_R] = expressions[FBExpression.Eyes_Look_Up_R] * 0.55f;
            expressions[FBExpression.Eyes_Look_Down_L] = expressions[FBExpression.Eyes_Look_Down_L] * 1.5f;
            expressions[FBExpression.Eyes_Look_Down_R] = expressions[FBExpression.Eyes_Look_Down_R] * 1.5f;

            expressions[FBExpression.Eyes_Look_Left_L] = expressions[FBExpression.Eyes_Look_Left_L] * 0.85f;
            expressions[FBExpression.Eyes_Look_Right_L] = expressions[FBExpression.Eyes_Look_Right_L] * 0.85f;
            expressions[FBExpression.Eyes_Look_Left_R] = expressions[FBExpression.Eyes_Look_Left_R] * 0.85f;
            expressions[FBExpression.Eyes_Look_Right_R] = expressions[FBExpression.Eyes_Look_Right_R] * 0.85f;

            // Hack: turn rots to looks
            // Yitch = 29(left)-- > -29(right)
            // Yaw = -27(down)-- > 27(up)

            if (pitch_L > 0)
            {
                expressions[FBExpression.Eyes_Look_Left_L] = Math.Min(1, (float)(pitch_L / 29.0)) * SRANIPAL_NORMALIZER;
                expressions[FBExpression.Eyes_Look_Right_L] = 0;
            }
            else
            {
                expressions[FBExpression.Eyes_Look_Left_L] = 0;
                expressions[FBExpression.Eyes_Look_Right_L] = Math.Min(1, (float)((-pitch_L) / 29.0)) * SRANIPAL_NORMALIZER;
            }

            if (yaw_L > 0)
            {
                expressions[FBExpression.Eyes_Look_Up_L] = Math.Min(1, (float)(yaw_L / 27.0)) * SRANIPAL_NORMALIZER;
                expressions[FBExpression.Eyes_Look_Down_L] = 0;
            }
            else
            {
                expressions[FBExpression.Eyes_Look_Up_L] = 0;
                expressions[FBExpression.Eyes_Look_Down_L] = Math.Min(1, (float)((-yaw_L) / 27.0)) * SRANIPAL_NORMALIZER;
            }


            if (pitch_R > 0)
            {
                expressions[FBExpression.Eyes_Look_Left_R] = Math.Min(1, (float)(pitch_R / 29.0)) * SRANIPAL_NORMALIZER;
                expressions[FBExpression.Eyes_Look_Right_R] = 0;
            }
            else
            {
                expressions[FBExpression.Eyes_Look_Left_R] = 0;
                expressions[FBExpression.Eyes_Look_Right_R] = Math.Min(1, (float)((-pitch_R) / 29.0)) * SRANIPAL_NORMALIZER;
            }

            if (yaw_R > 0)
            {
                expressions[FBExpression.Eyes_Look_Up_R] = Math.Min(1, (float)(yaw_R / 27.0)) * SRANIPAL_NORMALIZER;
                expressions[FBExpression.Eyes_Look_Down_R] = 0;
            }
            else
            {
                expressions[FBExpression.Eyes_Look_Up_R] = 0;
                expressions[FBExpression.Eyes_Look_Down_R] = Math.Min(1, (float)((-yaw_R) / 27.0)) * SRANIPAL_NORMALIZER;
            }
        }

        internal unsafe void Teardown()
        {
            cancellationTokenSource.Cancel();

            if (thread != null)
                thread.Abort();

            cancellationTokenSource.Dispose();

            if ((IntPtr)_faceState != IntPtr.Zero)
            {
                _faceState = (FaceState*)null;
                if (_mappedView != null)
                {
                    _mappedView.Dispose();
                    _mappedView = null;
                }
                if (_mappedFile != null)
                {
                    _mappedFile.Dispose();
                    _mappedFile = null;
                }
            }
            if (_faceStateEvent != null)
            {
                _faceStateEvent.Dispose();
                _faceStateEvent = null;
            }
            _isTracking = new bool?();
        }

        bool IsValid(float3 value) => IsValid(value.x) && IsValid(value.y) && IsValid(value.z);
        bool IsValid(floatQ value) => IsValid(value.x) && IsValid(value.y) && IsValid(value.z) && IsValid(value.w) && InRange(value.x, new float2(1, -1)) && InRange(value.y, new float2(1, -1)) && InRange(value.z, new float2(1, -1)) && InRange(value.w, new float2(1, -1));

        bool IsValid(float value) => !float.IsInfinity(value) && !float.IsNaN(value);

        bool InRange(float value, float2 range) => (value <= range.x && value >= range.y);

        public struct EyeGazeData
        {
            public bool isValid;
            public float3 position;
            public floatQ rotation;
            public float open;
            public float squeeze;
            public float wide;
            public float gazeConfidence;
        }

        public EyeGazeData GetEyeData(FBEye fbEye)
        {
            EyeGazeData eyeRet = new EyeGazeData();
            switch (fbEye)
            {
                case FBEye.Left:
                    eyeRet.position = new float3(expressions[FBExpression.LeftPos_x], -expressions[FBExpression.LeftPos_y], expressions[FBExpression.LeftPos_z]);
                    eyeRet.rotation = new floatQ(-expressions[FBExpression.LeftRot_x], -expressions[FBExpression.LeftRot_y], -expressions[FBExpression.LeftRot_z], expressions[FBExpression.LeftRot_w]);
                    eyeRet.open = MathX.Max(0, expressions[FBExpression.Eyes_Closed_L]);
                    eyeRet.squeeze = expressions[FBExpression.Lid_Tightener_L];
                    eyeRet.wide = expressions[FBExpression.Upper_Lid_Raiser_L];
                    eyeRet.isValid = IsValid(eyeRet.position);
                    return eyeRet;
                case FBEye.Right:
                    eyeRet.position = new float3(expressions[FBExpression.RightPos_x], -expressions[FBExpression.RightPos_y], expressions[FBExpression.RightPos_z]);
                    eyeRet.rotation = new floatQ(-expressions[FBExpression.LeftRot_x], -expressions[FBExpression.LeftRot_y], -expressions[FBExpression.LeftRot_z], expressions[FBExpression.RightRot_w]);
                    eyeRet.open = MathX.Max(0, expressions[FBExpression.Eyes_Closed_R]);
                    eyeRet.squeeze = expressions[FBExpression.Lid_Tightener_R];
                    eyeRet.wide = expressions[FBExpression.Upper_Lid_Raiser_R];
                    eyeRet.isValid = IsValid(eyeRet.position);
                    return eyeRet;
                default:
                    throw new Exception($"Invalid eye argument: {fbEye}");
            }
        }

        public void GetEyeExpressions(FBEye fbEye, Eye frooxEye)
        {
            frooxEye.PupilDiameter = 0.004f;

            switch (fbEye)
            {
                case FBEye.Left:
                    frooxEye.UpdateWithRotation(new floatQ(-expressions[FBExpression.LeftRot_x], -expressions[FBExpression.LeftRot_z], -expressions[FBExpression.LeftRot_y], expressions[FBExpression.LeftRot_w]));
                    frooxEye.RawPosition = new float3(expressions[FBExpression.LeftPos_x], expressions[FBExpression.LeftPos_y], expressions[FBExpression.LeftPos_z]);
                    frooxEye.Openness = MathX.Max(0, expressions[FBExpression.Eyes_Closed_L]);
                    frooxEye.Squeeze = expressions[FBExpression.Lid_Tightener_L];
                    frooxEye.Widen = expressions[FBExpression.Upper_Lid_Raiser_L];
                    frooxEye.Frown = expressions[FBExpression.Lip_Corner_Puller_L] - expressions[FBExpression.Lip_Corner_Depressor_L];
                    break;
                case FBEye.Right:
                    frooxEye.UpdateWithRotation(new floatQ(-expressions[FBExpression.RightRot_x], -expressions[FBExpression.RightRot_z], -expressions[FBExpression.RightRot_y], expressions[FBExpression.RightRot_w]));
                    frooxEye.RawPosition = new float3(expressions[FBExpression.RightPos_x], expressions[FBExpression.RightPos_y], expressions[FBExpression.RightPos_z]);
                    frooxEye.Openness = MathX.Max(0, expressions[FBExpression.Eyes_Closed_R]);
                    frooxEye.Squeeze = expressions[FBExpression.Lid_Tightener_R];
                    frooxEye.Widen = expressions[FBExpression.Upper_Lid_Raiser_R];
                    frooxEye.Frown = expressions[FBExpression.Lip_Corner_Puller_R] - expressions[FBExpression.Lip_Corner_Depressor_R];
                    break;
                case FBEye.Combined:
                    frooxEye.UpdateWithRotation(MathX.Slerp(new floatQ(expressions[FBExpression.LeftRot_x], expressions[FBExpression.LeftRot_y], expressions[FBExpression.LeftRot_z], expressions[FBExpression.LeftRot_w]), new floatQ(expressions[FBExpression.RightRot_x], expressions[FBExpression.RightRot_y], expressions[FBExpression.RightRot_z], expressions[FBExpression.RightRot_w]), 0.5f));
                    frooxEye.RawPosition = MathX.Average(new float3(expressions[FBExpression.LeftPos_x], expressions[FBExpression.LeftPos_z], expressions[FBExpression.LeftPos_y]), new float3(expressions[FBExpression.RightPos_x], expressions[FBExpression.RightPos_z], expressions[FBExpression.RightPos_y]));
                    frooxEye.Openness = MathX.Max(0, expressions[FBExpression.Eyes_Closed_R] + expressions[FBExpression.Eyes_Closed_R]) / 2.0f;
                    frooxEye.Squeeze = (expressions[FBExpression.Lid_Tightener_R] + expressions[FBExpression.Lid_Tightener_R]) / 2.0f;
                    frooxEye.Widen = (expressions[FBExpression.Upper_Lid_Raiser_R] + expressions[FBExpression.Upper_Lid_Raiser_R]) / 2.0f;
                    frooxEye.Frown = (expressions[FBExpression.Lip_Corner_Puller_R] - expressions[FBExpression.Lip_Corner_Depressor_R]) + (expressions[FBExpression.Lip_Corner_Puller_L] - expressions[FBExpression.Lip_Corner_Depressor_L]) / 2.0f;
                    break;
            }

            frooxEye.IsTracking = IsValid(frooxEye.RawPosition);
            frooxEye.IsTracking = IsValid(frooxEye.Direction);
            frooxEye.IsTracking = IsValid(frooxEye.Openness);
        }

        public void CollectDeviceInfos(DataTreeList list)
        {
            var eyeDataTreeDictionary = new DataTreeDictionary();
            eyeDataTreeDictionary.Add("Name", "Quest Pro Eye Tracking");
            eyeDataTreeDictionary.Add("Type", "Eye Tracking");
            eyeDataTreeDictionary.Add("Model", "Quest Pro");
            list.Add(eyeDataTreeDictionary);

            var mouthDataTreeDictionary = new DataTreeDictionary();
            mouthDataTreeDictionary.Add("Name", "Quest Pro Face Tracking");
            mouthDataTreeDictionary.Add("Type", "Lip Tracking");
            mouthDataTreeDictionary.Add("Model", "Quest Pro");
            list.Add(mouthDataTreeDictionary);
        }

        public void RegisterInputs(InputInterface inputInterface)
        {
            _input = inputInterface;
            eyes = new Eyes(_input, "Quest Pro Eye Tracking", false);
            mouth = new Mouth(_input, "Quest Pro Face Tracking", new MouthParameterGroup[] {
                MouthParameterGroup.JawOpen,
                MouthParameterGroup.JawPose,
                MouthParameterGroup.TonguePose,
                MouthParameterGroup.LipRaise,
                MouthParameterGroup.LipHorizontal,
                MouthParameterGroup.SmileFrown,
                MouthParameterGroup.MouthDimple,
                MouthParameterGroup.MouthPout,
                MouthParameterGroup.LipOverturn,
                MouthParameterGroup.LipOverUnder,
                MouthParameterGroup.LipStretchTighten,
                MouthParameterGroup.LipsPress,
                MouthParameterGroup.CheekPuffSuck,
                MouthParameterGroup.CheekRaise,
                MouthParameterGroup.ChinRaise,
                MouthParameterGroup.NoseWrinkle
            });
        }

        public void UpdateInputs(float deltaTime)
        {
            UpdateMouth(deltaTime);
            UpdateEyes(deltaTime);
        }

        void UpdateEye(Eye eye, EyeGazeData data)
        {
            bool _isValid = IsValid(data.open);
            _isValid &= IsValid(data.position);
            _isValid &= IsValid(data.wide);
            _isValid &= IsValid(data.squeeze);
            _isValid &= IsValid(data.rotation);
            _isValid &= eye.IsTracking;

            eye.IsTracking = _isValid;

            if (eye.IsTracking)
            {
                eye.UpdateWithRotation(MathX.Slerp(floatQ.Identity, data.rotation, VDFaceTracking.EyeMoveMult));
                eye.Openness = MathX.Pow(MathX.FilterInvalid(data.open, 0.0f), VDFaceTracking.EyeOpenExponent);
                eye.Widen = data.wide * VDFaceTracking.EyeWideMult;
            }
        }

        void UpdateEyes(float deltaTime)
        {
            eyes.IsEyeTrackingActive = _input.VR_Active;

            eyes.LeftEye.IsTracking = _input.VR_Active;

            var leftEyeData = VDFaceTracking.proxy.GetEyeData(FBEye.Left);
            var rightEyeData = VDFaceTracking.proxy.GetEyeData(FBEye.Right);

            eyes.LeftEye.IsTracking = leftEyeData.isValid;
            eyes.LeftEye.RawPosition = leftEyeData.position;
            eyes.LeftEye.PupilDiameter = 0.004f;
            eyes.LeftEye.Squeeze = leftEyeData.squeeze;
            eyes.LeftEye.Frown = expressions[FBExpression.Lip_Corner_Puller_L] - expressions[FBExpression.Lip_Corner_Depressor_L] * VDFaceTracking.EyeExpressionMult;
            eyes.LeftEye.InnerBrowVertical = expressions[FBExpression.Inner_Brow_Raiser_L];
            eyes.LeftEye.OuterBrowVertical = expressions[FBExpression.Outer_Brow_Raiser_L];
            eyes.LeftEye.Squeeze = expressions[FBExpression.Brow_Lowerer_L];

            UpdateEye(eyes.LeftEye, leftEyeData);

            eyes.RightEye.IsTracking = rightEyeData.isValid;
            eyes.RightEye.RawPosition = rightEyeData.position;
            eyes.RightEye.PupilDiameter = 0.004f;
            eyes.RightEye.Squeeze = rightEyeData.squeeze;
            eyes.RightEye.Frown = expressions[FBExpression.Lip_Corner_Puller_R] - expressions[FBExpression.Lip_Corner_Depressor_R] * VDFaceTracking.EyeExpressionMult;
            eyes.RightEye.InnerBrowVertical = expressions[FBExpression.Inner_Brow_Raiser_R];
            eyes.RightEye.OuterBrowVertical = expressions[FBExpression.Outer_Brow_Raiser_R];
            eyes.RightEye.Squeeze = expressions[FBExpression.Brow_Lowerer_R];

            UpdateEye(eyes.RightEye, rightEyeData);

            if (eyes.LeftEye.IsTracking || eyes.RightEye.IsTracking && (!eyes.LeftEye.IsTracking || !eyes.RightEye.IsTracking))
            {
                if (eyes.LeftEye.IsTracking)
                {
                    eyes.CombinedEye.RawPosition = eyes.LeftEye.RawPosition;
                    eyes.CombinedEye.UpdateWithRotation(eyes.LeftEye.RawRotation);
                }
                else
                {
                    eyes.CombinedEye.RawPosition = eyes.RightEye.RawPosition;
                    eyes.CombinedEye.UpdateWithRotation(eyes.RightEye.RawRotation);
                }
                eyes.CombinedEye.IsTracking = true;
            }
            else
            {
                eyes.CombinedEye.IsTracking = false;
            }

            eyes.CombinedEye.IsTracking = eyes.LeftEye.IsTracking || eyes.RightEye.IsTracking;
            eyes.CombinedEye.RawPosition = (eyes.LeftEye.RawPosition + eyes.RightEye.RawPosition) * 0.5f;
            eyes.CombinedEye.UpdateWithRotation(MathX.Slerp(eyes.LeftEye.RawRotation, eyes.RightEye.RawRotation, 0.5f));
            eyes.CombinedEye.PupilDiameter = 0.004f;

            eyes.LeftEye.Openness = MathX.Pow(1.0f - Math.Max(0, Math.Min(1, expressions[(int)Expressions.EyesClosedL] + expressions[(int)Expressions.EyesClosedL] * expressions[(int)Expressions.LidTightenerL])), VDFaceTracking.EyeOpenExponent);
            eyes.RightEye.Openness = MathX.Pow(1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)Expressions.EyesClosedR] + expressions[(int)Expressions.EyesClosedR] * expressions[(int)Expressions.LidTightenerR])), VDFaceTracking.EyeOpenExponent);

            eyes.ComputeCombinedEyeParameters();
            eyes.ConvergenceDistance = 0f;
            eyes.Timestamp += deltaTime;
            eyes.FinishUpdate();
        }

        void UpdateMouth(float deltaTime)
        {
            mouth.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
            mouth.IsTracking = Engine.Current.InputInterface.VR_Active;

            // Pulled from Resonite:
            mouth.IsTracking = true;
            mouth.MouthLeftSmileFrown = expressions[FBExpression.Lip_Corner_Puller_L] - expressions[FBExpression.Lip_Corner_Depressor_L];
            mouth.MouthRightSmileFrown = expressions[FBExpression.Lip_Corner_Puller_R] - expressions[FBExpression.Lip_Corner_Depressor_R];
            mouth.MouthLeftDimple = expressions[FBExpression.Dimpler_L];
            mouth.MouthRightDimple = expressions[FBExpression.Dimpler_R];
            mouth.CheekLeftPuffSuck = expressions[FBExpression.Cheek_Puff_L] - expressions[FBExpression.Cheek_Suck_L];
            mouth.CheekRightPuffSuck = expressions[FBExpression.Cheek_Puff_R] - expressions[FBExpression.Cheek_Suck_R];
            mouth.CheekLeftRaise = expressions[FBExpression.Cheek_Raiser_L];
            mouth.CheekRightRaise = expressions[FBExpression.Cheek_Raiser_R];
            mouth.LipUpperLeftRaise = expressions[FBExpression.Upper_Lip_Raiser_L];
            mouth.LipUpperRightRaise = expressions[FBExpression.Upper_Lip_Raiser_R];
            mouth.LipLowerLeftRaise = expressions[FBExpression.Lower_Lip_Depressor_L];
            mouth.LipLowerRightRaise = expressions[FBExpression.Lower_Lip_Depressor_R];
            mouth.MouthPoutLeft = expressions[FBExpression.Lip_Pucker_L];
            mouth.MouthPoutRight = expressions[FBExpression.Lip_Pucker_R];
            mouth.LipUpperHorizontal = expressions[FBExpression.Mouth_Right] - expressions[FBExpression.Mouth_Left];
            mouth.LipLowerHorizontal = mouth.LipUpperHorizontal;
            mouth.LipTopLeftOverturn = expressions[FBExpression.Lip_Funneler_LT];
            mouth.LipTopRightOverturn = expressions[FBExpression.Lip_Funneler_RT];
            mouth.LipBottomLeftOverturn = expressions[FBExpression.Lip_Funneler_LB];
            mouth.LipBottomRightOverturn = expressions[FBExpression.Lip_Funneler_RB];
            mouth.LipTopLeftOverUnder = -expressions[FBExpression.Lip_Suck_LT];
            mouth.LipTopRightOverUnder = -expressions[FBExpression.Lip_Suck_RT];
            mouth.LipBottomLeftOverUnder = -expressions[FBExpression.Lip_Suck_LB];
            mouth.LipBottomRightOverUnder = -expressions[FBExpression.Lip_Suck_RB];
            mouth.LipLeftStretchTighten = expressions[FBExpression.Lip_Stretcher_L] - expressions[FBExpression.Lid_Tightener_L];
            mouth.LipRightStretchTighten = expressions[FBExpression.Lip_Stretcher_R] - expressions[FBExpression.Lid_Tightener_R];
            mouth.LipsLeftPress = expressions[FBExpression.Lip_Pressor_L];
            mouth.LipsRightPress = expressions[FBExpression.Lip_Pressor_R];
            mouth.Jaw = new float3(expressions[FBExpression.Jaw_Sideways_Right] - expressions[FBExpression.Jaw_Sideways_Left], -expressions[FBExpression.Lips_Toward], expressions[FBExpression.Jaw_Thrust]);
            mouth.JawOpen = MathX.Clamp01(expressions[FBExpression.Jaw_Drop] - expressions[FBExpression.Lips_Toward]);
            mouth.Tongue = new float3(0f, 0f, expressions[FBExpression.TongueOut] - expressions[FBExpression.TongueRetreat]);
            mouth.NoseWrinkleLeft = expressions[FBExpression.Nose_Wrinkler_L];
            mouth.NoseWrinkleRight = expressions[FBExpression.Nose_Wrinkler_R];
            mouth.ChinRaiseBottom = expressions[FBExpression.Chin_Raiser_B];
            mouth.ChinRaiseTop = expressions[FBExpression.Chin_Raiser_T];
        }
    }

    public enum FBEye
    {
        Left,
        Right,
        Combined
    }
}