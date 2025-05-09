﻿using Lagrange.Core.Internal.Packets.Login;
using Lagrange.Core.Utility;
using Lagrange.Core.Utility.Cryptography;

namespace Lagrange.Core.Internal.Services.Login;

internal static class NTLoginCommon
{
    private const string Tag = nameof(NTLoginCommon);
    
    public static ReadOnlyMemory<byte> Encode(BotContext context, byte[] credential, (string Sig, string Rand, string Sid)? captcha)
    {
        if (context.Keystore.State.KeyExchangeSession is not { } session)
        {
            context.LogError(Tag, "Key exchange session is not initialized.");
            throw new InvalidOperationException("Key exchange session is not initialized.");
        }

        var request = new NTLoginRequest
        {
            Sig = credential,
            Captcha = captcha is { } value ? new NTLoginCaptcha
            {
                ProofWaterSig = value.Sig,
                ProofWaterRand = value.Rand,
                ProofWaterSid = value.Sid
            } : null
        };

        var login = new NTLogin
        {
            Head = new NTLoginHead
            {
                Account = new NTLoginAccount
                {
                    Account = context.Keystore.Uin.ToString()
                },
                System = new NTLoginSystem
                {
                    DevType = context.AppInfo.Os,
                    DevName = context.Keystore.DeviceName,
                    RegisterVendorType = 5,
                    Guid = context.Keystore.Guid
                },
                Version = new NTLoginVersion
                {
                    Version = context.AppInfo.Kernel,
                    AppId = context.AppInfo.AppId,
                    AppName = context.AppInfo.PackageName
                },
                Cookie = context.Keystore.State.Cookie is { } cookie ? new NTLoginCookie
                {
                    Cookie = cookie
                } : null
            }, 
            Body = ProtoHelper.Serialize(request)
        };
        
        var forward = new NTLoginForwardRequest
        {
            SessionTicket = session.SessionTicket,
            Buffer = AesGcmProvider.Encrypt(ProtoHelper.Serialize(login).Span, session.SessionKey),
            Type = 1
        };

        return ProtoHelper.Serialize(forward);
    }

    public static State Decode(BotContext context, ReadOnlyMemory<byte> payload, out NTLoginErrorInfo? info, out NTLoginResponse resp)
    {
        if (context.Keystore.State.KeyExchangeSession is not { } session)
        {
            context.LogError(Tag, "Key exchange session is not initialized.");
            throw new InvalidOperationException("Key exchange session is not initialized.");
        }
        
        var forward = ProtoHelper.Deserialize<NTLoginForwardRequest>(payload.Span);
        var buffer = AesGcmProvider.Decrypt(forward.Buffer, session.SessionKey);

        var login = ProtoHelper.Deserialize<NTLogin>(buffer);
        var state = (State)((info = login.Head.ErrorInfo)?.ErrorCode ?? 0);
        resp = ProtoHelper.Deserialize<NTLoginResponse>(login.Body.Span);
        
        if (state == State.LOGIN_ERROR_SUCCESS)
        {
            var credential = resp.Credentials;
            var wloginSigs = context.Keystore.WLoginSigs;
            
            wloginSigs.A1 = credential.A1;
            wloginSigs.A2 = credential.A2;
            wloginSigs.D2 = credential.D2;
            wloginSigs.D2Key = credential.D2Key;
        }

        if (login.Head.Cookie?.Cookie is { } cookie) context.Keystore.State.Cookie = cookie;
        
        return state;
    }

    public enum State
    {
        LOGIN_ERROR_ACCOUNT_NOT_UIN = 140022018,
        LOGIN_ERROR_ACCOUNT_OR_PASSWORD_ERROR = 140022013,
        LOGIN_ERROR_BLACK_ACCOUNT = 150022021,
        LOGIN_ERROR_DEFAULT = 140022000,
        LOGIN_ERROR_EXPIRE_TICKET = 140022014,
        LOGIN_ERROR_FROZEN = 140022005,
        LOGIN_ERROR_ILLAGE_TICKET = 140022016,
        LOGIN_ERROR_INVAILD_COOKIE = 140022012,
        LOGIN_ERROR_INVALID_PARAMETER = 140022001,
        LOGIN_ERROR_KICKED_TICKET = 140022015,
        LOGIN_ERROR_MUTIPLE_PASSWORD_INCORRECT = 150022029,
        LOGIN_ERROR_NEED_UPDATE = 140022004,
        LOGIN_ERROR_NEED_VERIFY_REAL_NAME = 140022019,
        LOGIN_ERROR_NEW_DEVICE = 140022010,
        LOGIN_ERROR_NICE_ACCOUNT_EXPIRED = 150022020,
        LOGIN_ERROR_NICE_ACCOUNT_PARENT_CHILD_EXPIRED = 150022025,
        LOGIN_ERROR_PASSWORD = 2,
        LOGIN_ERROR_PROOFWATER = 140022008,
        LOGIN_ERROR_PROTECT = 140022006,
        LOGIN_ERROR_REFUSE_PASSOWRD_LOGIN = 140022009,
        LOGIN_ERROR_REMIND_CANAEL_LATED_STATUS = 150022028,
        LOGIN_ERROR_SCAN = 1,
        LOGIN_ERROR_SUCCESS = 0,
        LOGIN_ERROR_SECBEAT = 140022017,
        LOGIN_ERROR_SMS_INVALID = 150022026,
        LOGIN_ERROR_STRICK = 140022007,
        LOGIN_ERROR_SYSTEM_FAILED = 140022002,
        LOGIN_ERROR_TGTGT_EXCHAGE_A1_FORBID = 150022027,
        LOGIN_ERROR_TIMEOUT_RETRY = 140022003,
        LOGIN_ERROR_TOO_MANY_TIMES_TODAY = 150022023,
        LOGIN_ERROR_TOO_OFTEN = 150022022,
        LOGIN_ERROR_UNREGISTERED = 150022024,
        LOGIN_ERROR_UNUSUAL_DEVICE = 140022011,
    }
}