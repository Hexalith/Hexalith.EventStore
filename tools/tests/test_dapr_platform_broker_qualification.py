"""Expose Platform controls to the established qualification discovery pattern."""
import test_dapr_platform_broker_configuration as configuration


def load_tests(loader, tests, pattern):
    return loader.loadTestsFromModule(configuration)
