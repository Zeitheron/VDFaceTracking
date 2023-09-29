namespace VDFaceTracking
{
    public static class FBExpression
    {
        public const int Brow_Lowerer_L = 0;
        public const int Brow_Lowerer_R = 1;
        public const int Cheek_Puff_L = 2;
        public const int Cheek_Puff_R = 3;
        public const int Cheek_Raiser_L = 4;
        public const int Cheek_Raiser_R = 5;
        public const int Cheek_Suck_L = 6;
        public const int Cheek_Suck_R = 7;
        public const int Chin_Raiser_B = 8;
        public const int Chin_Raiser_T = 9;
        public const int Dimpler_L = 10;
        public const int Dimpler_R = 11;
        public const int Eyes_Closed_L = 12;
        public const int Eyes_Closed_R = 13;
        public const int Eyes_Look_Down_L = 14;
        public const int Eyes_Look_Down_R = 15;
        public const int Eyes_Look_Left_L = 16;
        public const int Eyes_Look_Left_R = 17;
        public const int Eyes_Look_Right_L = 18;
        public const int Eyes_Look_Right_R = 19;
        public const int Eyes_Look_Up_L = 20;
        public const int Eyes_Look_Up_R = 21;
        public const int Inner_Brow_Raiser_L = 22;
        public const int Inner_Brow_Raiser_R = 23;
        public const int Jaw_Drop = 24;
        public const int Jaw_Sideways_Left = 25;
        public const int Jaw_Sideways_Right = 26;
        public const int Jaw_Thrust = 27;
        public const int Lid_Tightener_L = 28;
        public const int Lid_Tightener_R = 29;
        public const int Lip_Corner_Depressor_L = 30;
        public const int Lip_Corner_Depressor_R = 31;
        public const int Lip_Corner_Puller_L = 32;
        public const int Lip_Corner_Puller_R = 33;
        public const int Lip_Funneler_LB = 34;
        public const int Lip_Funneler_LT = 35;
        public const int Lip_Funneler_RB = 36;
        public const int Lip_Funneler_RT = 37;
        public const int Lip_Pressor_L = 38;
        public const int Lip_Pressor_R = 39;
        public const int Lip_Pucker_L = 40;
        public const int Lip_Pucker_R = 41;
        public const int Lip_Stretcher_L = 42;
        public const int Lip_Stretcher_R = 43;
        public const int Lip_Suck_LB = 44;
        public const int Lip_Suck_LT = 45;
        public const int Lip_Suck_RB = 46;
        public const int Lip_Suck_RT = 47;
        public const int Lip_Tightener_L = 48;
        public const int Lip_Tightener_R = 49;
        public const int Lips_Toward = 50;
        public const int Lower_Lip_Depressor_L = 51;
        public const int Lower_Lip_Depressor_R = 52;
        public const int Mouth_Left = 53;
        public const int Mouth_Right = 54;
        public const int Nose_Wrinkler_L = 55;
        public const int Nose_Wrinkler_R = 56;
        public const int Outer_Brow_Raiser_L = 57;
        public const int Outer_Brow_Raiser_R = 58;
        public const int Upper_Lid_Raiser_L = 59;
        public const int Upper_Lid_Raiser_R = 60;
        public const int Upper_Lip_Raiser_L = 61;
        public const int Upper_Lip_Raiser_R = 62;
        public const int Max = 63;
        
        // Above are the natural expressions tracked by the Quest Pro
        // Below is the eye tracking information
        public const int LeftRot_x = 64;
        public const int LeftRot_y = 65;
        public const int LeftRot_z = 66;
        public const int LeftRot_w = 67;
        public const int LeftPos_x = 68;
        public const int LeftPos_y = 70; // Flipped, need to convert RHS to LHS
        public const int LeftPos_z = 69;
        // public const int 71 is unused
        public const int RightRot_x = 72;
        public const int RightRot_y = 73;
        public const int RightRot_z = 74;
        public const int RightRot_w = 75;
        public const int RightPos_x = 76;
        public const int RightPos_y = 78; // Flipped, need to convert RHS to LHS
        public const int RightPos_z = 77;
    }
}
