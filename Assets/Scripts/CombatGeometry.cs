using System;

// Pure numeric contract shared by collision code and the standalone verification suite.
public static class CombatGeometry
{
    public static float SegmentDistanceSquared(float ax, float ay, float az, float bx, float by, float bz,
        float px, float py, float pz)
    {
        float dx=bx-ax, dy=by-ay, dz=bz-az;
        float length=dx*dx+dy*dy+dz*dz;
        float t=length>0.000001f?((px-ax)*dx+(py-ay)*dy+(pz-az)*dz)/length:0f;
        t=Math.Max(0f,Math.Min(1f,t));
        float x=ax+dx*t-px, y=ay+dy*t-py, z=az+dz*t-pz;
        return x*x+y*y+z*z;
    }
}
