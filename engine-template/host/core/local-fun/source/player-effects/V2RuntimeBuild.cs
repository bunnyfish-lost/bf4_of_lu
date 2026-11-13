using System;
public static class V2RuntimeBuild {
 public const string ClientHash="0365a62f197d4544b5326b82a682d77af542e973cb3d9cb6ae582cd543fae2d0";
 public const string ServerHash="160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba";
 public const string Build="v2-1107";
 public static bool IsClientHash(string value){return value==ClientHash;}
 public static bool AcceptsHash(string actual,string expected){return expected==ClientHash?actual==ClientHash:expected==ServerHash&&actual==ServerHash;}
}
