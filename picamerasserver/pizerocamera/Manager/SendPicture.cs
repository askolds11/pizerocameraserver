using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    /// <summary>
    /// Request to send picture
    /// </summary>
    /// <param name="ids">Provide a List for specific devices, null for global</param>
    public async Task RequestSendPicture(IEnumerable<string>? ids, Guid uuid)
    {
        var options = _optionsMonitor.CurrentValue;

        CameraRequest sendPictureRequest = new CameraRequest.SendPicture(
            uuid
        );

        if (ids == null)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic(options.CameraTopic)
                .WithPayload(Json.Serialize(sendPictureRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            var publishResult = await _mqttClient.PublishAsync(message);

            if (publishResult.IsSuccess)
            {
                foreach (var piZeroCamera in PiZeroCameras.Values)
                {
                    piZeroCamera.TakePictureRequest = new PiZeroCameraTakePictureRequest.RequestedSend();
                }
            }
            else
            {
                foreach (var piZeroCamera in PiZeroCameras.Values)
                {
                    piZeroCamera.TakePictureRequest =
                        new PiZeroCameraTakePictureRequest.FailedToRequestSend(publishResult.ReasonString);
                }
            }

            OnChange?.Invoke();
            return;
        }

        MqttApplicationMessage GetMessage(string id) =>
            new MqttApplicationMessageBuilder().WithContentType("application/json")
                .WithTopic($"{options.CameraTopic}/{id}")
                .WithPayload(Json.Serialize(sendPictureRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

        var idList = ids.ToList();

        // TODO: Maybe not

        // PiZeroCameras that are not requested set to null
        foreach (var id in PiZeroCameras.Keys.Except(idList))
        {
            PiZeroCameras[id].TakePictureRequest = null;
        }

        // PiZeroCameras that are requested
        var tasks = PiZeroCameras.Keys
            .Intersect(idList)
            .Select(async id => (id, await _mqttClient.PublishAsync(GetMessage(id))));


        var tasksResult = await Task.WhenAll(tasks);

        // Process message sending results
        foreach (var (id, publishResult) in tasksResult)
        {
            var piZeroCamera = PiZeroCameras[id];
            if (publishResult.IsSuccess)
            {
                piZeroCamera.TakePictureRequest = new PiZeroCameraTakePictureRequest.RequestedSend();
            }
            else
            {
                piZeroCamera.TakePictureRequest =
                    new PiZeroCameraTakePictureRequest.FailedToRequestSend(publishResult.ReasonString);
            }
        }

        OnChange?.Invoke();
    }

    public void ResponseSendPicture(MqttApplicationMessage message, CameraResponse.SendPicture sendPicture)
    {
        var id = message.Topic.Split('/').Last();

        var piZeroCamera = PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);

        var successWrapper = sendPicture.Response;

        // if (response.IsSuccess)
        // {
        // var successWrapper = response.Value;
        if (successWrapper.Success)
        {
            piZeroCamera.TakePictureRequest = successWrapper.Value switch
            {
                SendPictureResponse.PictureSent =>
                    new PiZeroCameraTakePictureRequest.Success(),
                _ => new PiZeroCameraTakePictureRequest.Unknown("Unknown success")
            };
        }
        else
        {
            piZeroCamera.TakePictureRequest = successWrapper.Value switch
            {
                SendPictureResponse.Failure failure =>
                    new PiZeroCameraTakePictureRequest.FailureSend(failure),
                _ => new PiZeroCameraTakePictureRequest.Unknown("Unknown failure")
            };
        }
        // }
        // else
        // {
        //     piZeroCamera.TakePictureRequest = new PiZeroCameraTakePictureRequest.Unknown(response.Error.ToString());
        // }

        OnChange?.Invoke();
    }
}