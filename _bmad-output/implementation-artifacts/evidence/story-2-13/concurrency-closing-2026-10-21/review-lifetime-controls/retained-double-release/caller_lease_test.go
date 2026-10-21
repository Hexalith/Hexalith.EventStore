package router
import (
 "context"
 "net"
 "testing"
 "time"
 "github.com/dapr/dapr/pkg/actors/api"
 "github.com/dapr/dapr/pkg/api/grpc/manager"
 "github.com/dapr/dapr/pkg/modes"
 securityfake "github.com/dapr/dapr/pkg/security/fake"
 "github.com/stretchr/testify/require"
 "google.golang.org/grpc"
 grpcbackoff "google.golang.org/grpc/backoff"
 "google.golang.org/grpc/codes"
 "google.golang.org/grpc/connectivity"
 "google.golang.org/grpc/status"
)
func TestReviewRetiringCallerKeepsOtherLease(t *testing.T) {
 ctx,cancel:=context.WithTimeout(t.Context(),5*time.Second); defer cancel()
 listener,err:=net.Listen("tcp","127.0.0.1:0"); require.NoError(t,err)
 address:=listener.Addr().String(); require.NoError(t,listener.Close())
 mgr:=manager.NewManager(securityfake.New(),modes.StandaloneMode,&manager.AppChannelConfig{})
 conn,release,err:=mgr.GetGRPCConnection(ctx,address,"target","default",grpc.WithConnectParams(grpc.ConnectParams{Backoff:grpcbackoff.Config{BaseDelay:time.Hour,Multiplier:1,MaxDelay:time.Hour},MinConnectTimeout:time.Second}))
 require.NoError(t,err); defer conn.Close(); defer release(false)
 conn.Connect(); require.Eventually(t,func()bool{return conn.GetState()==connectivity.TransientFailure},time.Second,time.Millisecond)
 r:=&router{grpc:mgr,namespace:"default"}
 _,err=r.callRemoteActor(ctx,&api.LookupActorResponse{Address:address,AppID:"target"},concurrencyRequest("review-other-lease"))
 require.Equal(t,codes.Unavailable,status.Code(err))
 require.Equal(t,connectivity.TransientFailure,conn.GetState(),"router must release only its own lease while another lease stays held")
 next,teardown,err:=mgr.GetGRPCConnection(ctx,address,"target","default"); require.NoError(t,err); defer teardown(true)
 require.NotSame(t,conn,next,"new leases exclude the retired channel while the old lease drains")
}
