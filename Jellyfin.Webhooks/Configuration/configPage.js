define(['loading', 'confirm', 'globalize', 'dom', 'dashboardcss', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox'], function (loading, confirm, globalize, dom) {
    'use strict';

    const pluginUniqueId = 'd8ca599b-ab3c-41b0-a4ea-6de1d52b9996';

    function onViewShow() {
        loadData(this);
    }

    function onAddWebhookClick() {
        const page = Dashboard.getConfigurationPageUrl('Webhooks.Editor');
        Dashboard.navigate(page);
        return false;
    }

    function onDeleteWebhookClick() {
        const view = dom.parentWithClass(this, 'page');
        const id = this.getAttribute('data-hookid')
        confirm('Do you really want delete this webhook?', 'Delete Webhook').then(() => {
            loading.show();
            ApiClient.ajax({
                type: 'DELETE',
                url: ApiClient.getUrl('Webhooks', { Id: id })
            }).then(function () {
                loadData(view);
            });
        });
    }

    function onEditWebhookClick() {
        const id = this.getAttribute('data-hookid')
        const page = Dashboard.getConfigurationResourceUrl('Webhooks.Editor');
        Dashboard.navigate(page + '&id=' + id);
        return false;
    }

    function getEventsHtml(hook) {
        let result = '';
        result += hook.Events.join(', ');
        return result;
    }

    function getHookHtml(hook) {
        let result = '';
        result += '<div class="listItem listItem-border">';
        result += '<div class="listItemBody three-line" data-hookid="' + hook.Id + '">';
        result += '<div class="listItemBodyText">' + hook.Url + '</div>';
        result += '<div class="listItemBodyText secondary">';
        result += getEventsHtml(hook);
        result += '</div>'; // secondary
        result += '<div class="listItemBodyText secondary">' + hook.Format + '</div>';
        result += '</div>'; // listItemBody
        result += '<button type="button" is="paper-icon-button-light" class="btn-webhook-delete paper-icon-button-light"';
        result += ' data-hookid="' + hook.Id + '" title="' + globalize.translate('ButtonDelete') + '">';
        result += '<i class="md-icon">delete</i></button>';
        result += '</div>'; // listItem
        return result;
    }

    function loadData(view) {
        loading.show();
        ApiClient.getPluginConfiguration(pluginUniqueId).then(config => {
            const webhooks = view.querySelector('.webhooks')
            webhooks.innerHTML = config.Hooks.map(getHookHtml).join('');
            webhooks.querySelectorAll('.webhooks .listItemBody')
                .forEach(el => el.addEventListener('click', onEditWebhookClick));
            webhooks.querySelectorAll('.btn-webhook-delete')
                .forEach(el => el.addEventListener('click', onDeleteWebhookClick));
            loading.hide();
        });
    }

    return function (view, params) {
        view.addEventListener('viewshow', onViewShow);
        view.querySelector('.btn-webhook-add').addEventListener('click', onAddWebhookClick);
    };
});
